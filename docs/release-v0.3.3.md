# Magpie-Rhino 0.3.3

YOLO is now a remembered execution mode. Enable **YOLO 自动执行** once to skip
routine tool confirmations across new conversations, document changes and Rhino
restarts. The footer identifies the active mode. Switch it off in settings to
return to per-tool confirmation.
Changes synchronize across live panels; stale browser settings cannot re-enable
YOLO after another panel disables it.

The preference is stored in the dedicated
`~/.config/rhino9-magpie/panel-preferences.json`; gateway connection credentials
remain in their existing file. Fresh installations default to confirmation.
Design questions and exact-note scale-assumption approval remain independent.

Reading expanded tool or assistant details suspends automatic following.
Stream updates preserve the reading position and nested detail scrolling.
Use the jump-to-latest button to resume following.

The marketplace README installation column now uses the correct `rhino9-ai`
package slug, while the displayed product name remains **Magpie-Rhino**.

Local validation includes 43 agent checks, 157 workflow checks, 20 browser
regressions, embedded-WebView expansion/wheel checks and native YOLO
execution returning `YOLO_MODE_OK 11` without changing document geometry.
