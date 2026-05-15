// Statusline script for Claude Monitor. Never throws — failure must be silent so
// Claude Code's own statusline rendering is not broken by us.
try {
  const fs = require("fs");
  const path = require("path");
  const os = require("os");

  const SL_VERSION = "1.0.5";

  function readJson(p) {
    const raw = fs.readFileSync(p, "utf8").replace(/^﻿/, "");
    return JSON.parse(raw);
  }

  // Anthropic may switch resets_at to milliseconds at any point. 10^12 seconds
  // is year 33658, so anything past that is unambiguously ms.
  function normalizeEpochSeconds(epoch) {
    if (!epoch || epoch <= 0) return 0;
    return epoch > 1e12 ? Math.floor(epoch / 1000) : epoch;
  }

  // Infer context window size from model id if Claude Code didn't include it.
  // Hard-coding all model sizes is brittle; we cover the common cases.
  function inferContextWindowSize(modelId) {
    if (!modelId) return null;
    const id = String(modelId).toLowerCase();
    if (id.includes("1m") || id.includes("-1m")) return 1_000_000;
    if (id.includes("opus") || id.includes("sonnet") || id.includes("haiku")) return 200_000;
    return null;
  }

  let d;
  try { d = readJson(0); } catch (e) { process.exit(0); }

  let cfg = {};
  try {
    const cfgPath = path.join(process.env.USERPROFILE || process.env.HOME || os.homedir(), ".claude", "widget-settings.json");
    if (fs.existsSync(cfgPath)) cfg = readJson(cfgPath);
  } catch (e) {}
  const show = (key) => cfg[key] !== false;

  // Context window size: prefer payload, then infer from model id, then fall back to 200k.
  const cwSize =
    d.context_window?.context_window_size ||
    inferContextWindowSize(d.model?.id) ||
    200000;

  // Tokens actually occupying the context window for the next turn:
  //   input_tokens          — uncached portion of this turn's input
  //   cache_creation_*      — portion newly written to prompt cache
  //   cache_read_*          — portion served from prompt cache
  // We DO NOT add output_tokens here: Anthropic's `input_tokens` on the next
  // turn already includes the previous turn's output (as part of conversation
  // history), so adding output would double-count it.
  const cu = d.context_window?.current_usage;
  const usedTokens = cu
    ? (cu.input_tokens || 0) + (cu.cache_creation_input_tokens || 0) + (cu.cache_read_input_tokens || 0)
    : 0;
  const ctx = usedTokens > 0
    ? Math.min(100, Math.round((usedTokens / cwSize) * 100))
    : Math.round(d.context_window?.used_percentage || 0);

  // Save per-session data for the widget — atomic write (tmp + rename).
  try {
    const dir = path.join(process.env.USERPROFILE || process.env.HOME || os.homedir(), ".claude", "widget-sessions");
    if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
    const sid = (d.session_id || "unknown").replace(/[^a-zA-Z0-9_-]/g, "");
    if (sid) {
      const payload = { ...d, _ts: Date.now(), _sl_version: SL_VERSION };
      if (payload.context_window) {
        payload.context_window.used_percentage = ctx;
        payload.context_window.context_window_size = cwSize;
      }
      // Normalize resets_at so the widget can rely on Unix seconds regardless
      // of what Anthropic sends.
      if (payload.rate_limits?.five_hour) {
        payload.rate_limits.five_hour.resets_at = rl5reset;
      }
      if (payload.rate_limits?.seven_day) {
        payload.rate_limits.seven_day.resets_at = rl7reset;
      }
      const dst = path.join(dir, `${sid}.json`);
      const tmp = `${dst}.${process.pid}.tmp`;
      fs.writeFileSync(tmp, JSON.stringify(payload));
      fs.renameSync(tmp, dst);
    }
  } catch (e) {}

  const model = d.model?.display_name || "?";
  const cost = (d.cost?.total_cost_usd || 0).toFixed(2);
  const durMs = d.cost?.total_duration_ms || 0;
  const added = d.cost?.total_lines_added || 0;
  const removed = d.cost?.total_lines_removed || 0;
  const totalIn = d.context_window?.total_input_tokens || 0;
  const totalOut = d.context_window?.total_output_tokens || 0;
  const rl5 = d.rate_limits?.five_hour?.used_percentage;
  const rl7 = d.rate_limits?.seven_day?.used_percentage;
  const rl5reset = normalizeEpochSeconds(d.rate_limits?.five_hour?.resets_at);
  const rl7reset = normalizeEpochSeconds(d.rate_limits?.seven_day?.resets_at);

  const durS = Math.floor(durMs / 1000);
  const dd = Math.floor(durS / 86400);
  const hh = Math.floor(durS % 86400 / 3600);
  const mm = Math.floor(durS % 3600 / 60);
  const ss = durS % 60;
  const duration = dd > 0 ? `${dd}d${hh}h${mm}m` : hh > 0 ? `${hh}h${mm}m` : mm > 0 ? `${mm}m${ss}s` : `${ss}s`;

  const barLen = 15;
  const filled = Math.round(ctx * barLen / 100);
  const empty = barLen - filled;
  const ctxColor = ctx < 50 ? "\x1b[36m" : ctx < 80 ? "\x1b[33m" : "\x1b[31m";
  const bar = ctxColor + "█".repeat(filled) + "░".repeat(empty) + "\x1b[0m";

  const costColor = cost > 1 ? "\x1b[33m" : "\x1b[32m";

  function resetIn(epoch) {
    if (!epoch) return "";
    const diff = Math.max(0, Math.floor(epoch - Date.now() / 1000));
    const D = Math.floor(diff / 86400);
    const H = Math.floor(diff % 86400 / 3600);
    const M = Math.floor(diff % 3600 / 60);
    if (D > 0) return `${D}d${H}h`;
    if (H > 0) return `${H}h${M}m`;
    return `${M}m`;
  }

  function fmtTokens(n) {
    if (n >= 1000000) return (n / 1000000).toFixed(1) + "M";
    if (n >= 1000) return (n / 1000).toFixed(1) + "k";
    return String(n);
  }

  const sep = "  │  ";
  const parts = [];

  if (show("sl_model"))   parts.push(`\x1b[1;35m${model}\x1b[0m`);
  if (show("sl_context")) parts.push(`${bar} ${ctx}%`);
  if (show("sl_cost"))    parts.push(`${costColor}$${cost}\x1b[0m`);
  if (show("sl_time"))    parts.push(`\x1b[36m${duration}\x1b[0m`);
  if (show("sl_tokens"))  parts.push(`\x1b[33m↓${fmtTokens(totalIn)}  ↑${fmtTokens(totalOut)}\x1b[0m`);
  if (show("sl_lines"))   parts.push(`\x1b[32m+${added}\x1b[0m/\x1b[31m-${removed}\x1b[0m`);

  if (show("sl_limits")) {
    if (rl5 != null) {
      const v = Math.round(rl5);
      const c = v < 50 ? "\x1b[32m" : v < 80 ? "\x1b[33m" : "\x1b[31m";
      const reset = rl5reset ? ` ~${resetIn(rl5reset)}` : "";
      parts.push(`${c}5h: ${v}%${reset}\x1b[0m`);
    }
    if (rl7 != null) {
      const v = Math.round(rl7);
      const c = v < 50 ? "\x1b[32m" : v < 80 ? "\x1b[33m" : "\x1b[31m";
      const reset = rl7reset ? ` ~${resetIn(rl7reset)}` : "";
      parts.push(`${c}7d: ${v}%${reset}\x1b[0m`);
    }
  }

  process.stdout.write(parts.join(sep));
} catch (e) {
  process.exit(0);
}
