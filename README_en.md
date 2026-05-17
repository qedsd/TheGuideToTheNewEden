[Chinese](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/README.md "Chinese Readme") | [English](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/README_en.md "English Readme")

# The Guide to the New Eden

This is a free, open-source collection of helper tools for the game EVE Online, covering various functions such as business management, alert reminders, multi-client previews, and guide queries. All features comply with the game's End User License Agreement (EULA), so please feel free to use them.

Some features and implementation ideas of this software are derived from the development achievements of community predecessors, to whom we extend our sincere respect and gratitude. We also sincerely thank all players who actively provided ideas and helped identify issues during the development process.

![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/en/Home_Dark.png?raw=true)

## Runtime Environment
- **Operating System:** Windows 10 19041.0 or higher, Windows 11 recommended. Does not support any LTSC, special edition, or slimmed-down Windows systems. Does not support Mac or Linux systems. No mobile version.
- **.NET Desktop Runtime 9.0:** [.NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

## Download
[Releases](https://github.com/qedsd/TheGuideToTheNewEden/releases)

---

# Features
- **Character:** Manage character authorization center, view character personal info such as skill points, skill training queue, wallet balance, wallet logs, contracts, mail, industry.
- **Market:** View market orders and historical prices for various solar systems or player structures.
- **Orders:** View personal and corporation order lists.
- **Trade Route:** Compare and recommend profit margins for different items between two markets.
- **Overview:** Display the game view in an always-on-top small window, commonly used to view and quickly switch between other character game windows while multi-boxing, similar to eve-o-preview.
- **Intel:** Monitor chat channels for real-time notifications of hostile locations, similar to SMT or Near2.
- **Chat Monitor:** Monitor chat channel messages and send notifications for information matching specific criteria.
- **Stats:** Count player factions in a channel, and query each player's threat level, common ships, etc., via ZKillboard.
- **Price Check:** Send an item name in the in-game chat channel, and the software will pop up a window showing the item's market price.
- **Log Monitor:** Monitor game combat logs and anomaly logs, sending notifications for information matching specific criteria.
- **Translation:** Query English-Chinese translations of game-specific terms, and perform general text translation after incorporating the game-specific term database.
- **DED Expedition:** Guides for some DED expeditions (some guides may be outdated; use for reference only).
- **Mission Guide:** Mission guides from the ancient server wiki (mostly outdated).
- **Wormhole:** View wormhole classes, static connections, wandering connections information.
- **Quick Links:** A collection of commonly used official and third-party website links.
- **Map:** In-game star map, viewable by security status, sovereignty ownership, planetary resources, includes navigation tools, sovereignty plugin requirements table, etc.
- **ZKillboard:** A youth version of the killboard using the open API from zkillboard.
- **Database:** View software database information.
- **Quick Input:** Typing window to avoid candidate word invisibility with built-in input methods.
- **Latency:** Test network latency between your computer and the game server.
- **Key Press Interval:** Monitor the time elapsed and number of presses for specified keys, dedicated for torpedo counting.

## Screenshots
- Light Mode
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/en/Home_Light.png?raw=true)

- Character Hub
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/en/Characters.png?raw=true)

- Intel
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/en/Intel.png?raw=true)

- Market
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/en/Market.png?raw=true)

- Overview (Multi-client preview)
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/en/Overview.png?raw=true)

- Trade Route
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/en/Business.png?raw=true)

- ZKillboard
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/en/ZKB.png?raw=true)

- Map
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/en/Map.png?raw=true)

# Project Structure
- **TheGuideToTheNewEden.Core**: A core library based on .NET Standard 2.1, containing platform-agnostic code such as Models, Helpers, database management, etc.
- **TheGuideToTheNewEden.WinUI**: The main project of the software, responsible for UI interaction and data processing, based on Windows App SDK + WinUI 3 + .NET 9. This is the **startup project** of the solution. Start here if you want to debug or modify the software code.
- TheGuideToTheNewEden.ServerLogger: Leftover from V1, currently not enabled. Used to record hourly online player counts and daily subreddit activity on a server.
- ZKB.NET: A .NET library that wraps the Zkillboard API for one-call access. ZKB.Net.Test is the test project for this library.
- TheGuideToTheNewEden.PreviewWindow: Multi-client preview window implemented using WPF. Uses memory mapping to establish communication between WPF and WINUI. Specific communication implementation is in the project TheGuideToTheNewEden.PreviewIPC.
- TheGuideToTheNewEden.ExefileSimulation: Testing tool, game simulator. Simulates game processes for multi-client preview testing, simulates chat channel auto-messaging, simulates game logs.
- TheGuideToTheNewEden.DevTools: Configuration generation tool. Used to generate various data for the software to use as configuration files, e.g., generating a JSON file of positional relationships between solar systems.
- TheGuideToTheNewEden.SystemCheck: Testing tool. Checks if the solar system names in the database match those from the official API.
- TheGuideToTheNewEden.WormholeCrawler: Configuration generation tool. Crawls wormhole information from websites.
- TheGuideToTheNewEden.SDEBuilder: Converts the official SDE (Static Data Export) into an SQLite database, generating the required Chinese/English database for the software with one click.
- TheGuideToTheNewEden.CrashReporter: Software crash reporter.

# The New Eden Past
## V1
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/homeV1.jpg?raw=true)
## V2
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/HomeV2.png?raw=true)

# Future Plans
The software will continue to have bugs fixed and more features added. Suggestions and bug reports are welcome, but update frequency cannot be guaranteed.

# Contact Me
- In-game ID: QEDSD
- [QQ Group: 784194289](https://jq.qq.com/?_wv=1027&k=m8Ttv1DX)
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/qq.jpg?raw=true)
