const fs = require("fs");
const path = require("path");
const d = JSON.parse(fs.readFileSync(0, "utf8"));

// Load widget settings for statusline toggles
let cfg = {};
try {
  const cfgPath = path.join(process.env.USERPROFILE || process.env.HOME, ".claude", "widget-settings.json");
  if (fs.existsSync(cfgPath)) cfg = JSON.parse(fs.readFileSync(cfgPath, "utf8"));
} catch(e) {}
const show = (key) => cfg[key] !== false; // default true

// Calculate context usage from actual current tokens
const cwSize = d.context_window?.context_window_size || 200000;
const cu = d.context_window?.current_usage;
const usedTokens = cu
  ? (cu.input_tokens || 0) + (cu.output_tokens || 0) + (cu.cache_creation_input_tokens || 0) + (cu.cache_read_input_tokens || 0)
  : 0;
const ctx = usedTokens > 0 ? Math.round(usedTokens / cwSize * 100) : Math.round(d.context_window?.used_percentage || 0);

// Save per-session data for widget
try {
  const dir = path.join(process.env.USERPROFILE || process.env.HOME, ".claude", "widget-sessions");
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  const sid = (d.session_id || "unknown").replace(/[^a-zA-Z0-9_-]/g, "");
  const payload = { ...d, _ts: Date.now() };
  if (payload.context_window) payload.context_window.used_percentage = ctx;
  fs.writeFileSync(path.join(dir, `${sid}.json`), JSON.stringify(payload));
} catch(e) {}

const model = d.model?.display_name || "?";
const cost = (d.cost?.total_cost_usd || 0).toFixed(2);
const durMs = d.cost?.total_duration_ms || 0;
const added = d.cost?.total_lines_added || 0;
const removed = d.cost?.total_lines_removed || 0;
const totalIn = d.context_window?.total_input_tokens || 0;
const totalOut = d.context_window?.total_output_tokens || 0;
const rl5 = d.rate_limits?.five_hour?.used_percentage;
const rl7 = d.rate_limits?.seven_day?.used_percentage;
const rl5reset = d.rate_limits?.five_hour?.resets_at;
const rl7reset = d.rate_limits?.seven_day?.resets_at;

// Duration
const durS = Math.floor(durMs / 1000);
const dd = Math.floor(durS / 86400);
const hh = Math.floor(durS % 86400 / 3600);
const mm = Math.floor(durS % 3600 / 60);
const ss = durS % 60;
const duration = dd > 0 ? `${dd}d${hh}h${mm}m` : hh > 0 ? `${hh}h${mm}m` : mm > 0 ? `${mm}m${ss}s` : `${ss}s`;

// Context bar
const barLen = 15;
const filled = Math.round(ctx * barLen / 100);
const empty = barLen - filled;
const ctxColor = ctx < 50 ? "\x1b[36m" : ctx < 80 ? "\x1b[33m" : "\x1b[31m";
const bar = ctxColor + "█".repeat(filled) + "░".repeat(empty) + "\x1b[0m";

// Cost color
const costColor = cost > 1 ? "\x1b[33m" : "\x1b[32m";

// Reset timer helper
function resetIn(epoch) {
  if (!epoch) return "";
  const diff = Math.max(0, Math.floor(epoch - Date.now() / 1000));
  const d = Math.floor(diff / 86400);
  const h = Math.floor(diff % 86400 / 3600);
  const m = Math.floor(diff % 3600 / 60);
  if (d > 0) return `${d}d${h}h`;
  if (h > 0) return `${h}h${m}m`;
  return `${m}m`;
}

// Token formatter
function fmtTokens(n) {
  if (n >= 1000000) return (n / 1000000).toFixed(1) + "M";
  if (n >= 1000) return (n / 1000).toFixed(1) + "k";
  return String(n);
}

// Build output parts
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
