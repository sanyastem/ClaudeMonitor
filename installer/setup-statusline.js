// Post-install script: configures Claude Code statusline
const fs = require("fs");
const path = require("path");
const os = require("os");

const home = process.env.USERPROFILE || process.env.HOME || os.homedir();
const claudeDir = path.join(home, ".claude");
const settingsPath = path.join(claudeDir, "settings.json");
const statuslineSrc = path.join(process.argv[2] || ".", "statusline.js");
const statuslineDst = path.join(claudeDir, "statusline.js");
const sessionsDir = path.join(claudeDir, "widget-sessions");

[claudeDir, sessionsDir].forEach(d => {
  if (!fs.existsSync(d)) fs.mkdirSync(d, { recursive: true });
});

if (fs.existsSync(statuslineSrc)) {
  fs.copyFileSync(statuslineSrc, statuslineDst);
  console.log("Copied statusline.js to " + statuslineDst);
}

let settings = {};
if (fs.existsSync(settingsPath)) {
  try {
    const raw = fs.readFileSync(settingsPath, "utf8").replace(/^﻿/, "");
    settings = JSON.parse(raw);
  } catch (e) {
    console.warn("Could not parse existing settings.json — leaving file unchanged.");
    process.exit(0);
  }
}

const slPath = statuslineDst.replace(/\\/g, "/");
const ourCommand = `node "${slPath}"`;
const existing = settings.statusLine && settings.statusLine.command;

if (existing && existing !== ourCommand && !existing.includes("/.claude/statusline.js")) {
  // User has a custom statusLine — back it up rather than clobbering it silently.
  settings.statusLine_backup = settings.statusLine;
  console.warn("Existing custom statusLine.command saved to settings.statusLine_backup");
}

settings.statusLine = { type: "command", command: ourCommand };

// Atomic write: tmp + rename
const tmp = `${settingsPath}.${process.pid}.tmp`;
fs.writeFileSync(tmp, JSON.stringify(settings, null, 2));
fs.renameSync(tmp, settingsPath);
console.log("Updated settings.json with statusline config");
console.log("Done!");
