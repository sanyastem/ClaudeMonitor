const fs = require("fs");
const path = require("path");
const d = JSON.parse(fs.readFileSync(0, "utf8"));

// Save per-session data for widget
try {
  const dir = path.join(process.env.USERPROFILE || process.env.HOME, ".claude", "widget-sessions");
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  const sid = (d.session_id || "unknown").replace(/[^a-zA-Z0-9_-]/g, "");
  fs.writeFileSync(path.join(dir, `${sid}.json`), JSON.stringify({ ...d, _ts: Date.now() }));
} catch(e) {}

const model = d.model?.display_name || "?";
const ctx = Math.round(d.context_window?.used_percentage || 0);
const cost = (d.cost?.total_cost_usd || 0).toFixed(2);
const durMs = d.cost?.total_duration_ms || 0;
const added = d.cost?.total_lines_added || 0;
const removed = d.cost?.total_lines_removed || 0;
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

// Rate limits
let rl = "";
if (rl5 != null) {
  const v = Math.round(rl5);
  const c = v < 50 ? "\x1b[32m" : v < 80 ? "\x1b[33m" : "\x1b[31m";
  const reset = rl5reset ? ` ~${resetIn(rl5reset)}` : "";
  rl += `  │  ${c}5h: ${v}%${reset}\x1b[0m`;
}
if (rl7 != null) {
  const v = Math.round(rl7);
  const c = v < 50 ? "\x1b[32m" : v < 80 ? "\x1b[33m" : "\x1b[31m";
  const reset = rl7reset ? ` ~${resetIn(rl7reset)}` : "";
  rl += `  │  ${c}7d: ${v}%${reset}\x1b[0m`;
}

const sep = "  │  ";

process.stdout.write(
  `\x1b[1;35m${model}\x1b[0m${sep}${bar} ${ctx}%${sep}${costColor}$${cost}\x1b[0m${sep}\x1b[36m${duration}\x1b[0m${sep}\x1b[32m+${added}\x1b[0m/\x1b[31m-${removed}\x1b[0m${rl}`
);
