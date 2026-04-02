// Post-install script: configures Claude Code statusline
const fs = require("fs");
const path = require("path");

const home = process.env.USERPROFILE || process.env.HOME;
const claudeDir = path.join(home, ".claude");
const settingsPath = path.join(claudeDir, "settings.json");
const statuslineSrc = path.join(process.argv[2] || ".", "statusline.js");
const statuslineDst = path.join(claudeDir, "statusline.js");
const sessionsDir = path.join(claudeDir, "widget-sessions");

// Ensure directories exist
[claudeDir, sessionsDir].forEach(d => {
  if (!fs.existsSync(d)) fs.mkdirSync(d, { recursive: true });
});

// Copy statusline.js
if (fs.existsSync(statuslineSrc)) {
  fs.copyFileSync(statuslineSrc, statuslineDst);
  console.log("Copied statusline.js to " + statuslineDst);
}

// Update settings.json
let settings = {};
if (fs.existsSync(settingsPath)) {
  try { settings = JSON.parse(fs.readFileSync(settingsPath, "utf8")); } catch(e) {}
}

const nodePath = process.execPath.replace(/\\/g, "/");
const slPath = statuslineDst.replace(/\\/g, "/");

settings.statusLine = {
  type: "command",
  command: `node ${slPath}`
};

fs.writeFileSync(settingsPath, JSON.stringify(settings, null, 2));
console.log("Updated settings.json with statusline config");
console.log("Done!");
