# Magpie-Rhino 0.3.2

Fix a model picker that initially showed only “Follow Magpie” even when many
routing groups were available.

- Load the gateway catalog automatically after the panel connects.
- Refresh stale catalogs when opening the picker; preserve the current choice.
- Show routing-group/model counts, an in-picker refresh button and visible
  loading/failure feedback. Bound catalog requests to 20 seconds.

Native regression reproduced 0.3.1 opening with one placeholder and zero groups.
The installed 0.3.2 panel opened without manual refresh and loaded all 80 routing
groups and 484 eligible model/strategy entries returned by the test gateway.
Its rendered picker contained 485 options including the default follow entry.
Available counts depend on the user's Magpie configuration.
