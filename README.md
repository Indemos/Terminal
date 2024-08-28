# Trading Terminal and Backtester

All-in-one. 
Trading terminal with generic gateway implementation, tick backtester, charting, and performance evaluator for trading strategies.
Supports stocks, FX, options, and futures with experimental support for crypto-currencies. 

# Status 

![GitHub Workflow Status (with event)](https://img.shields.io/github/actions/workflow/status/Indemos/Terminal/dotnet.yml?event=push)
![GitHub](https://img.shields.io/github/license/Indemos/Terminal)
![GitHub](https://img.shields.io/badge/system-Windows%20%7C%20Linux%20%7C%20Mac-blue)

# Disclaimer

The app is in active development state and can be updated without any notice. 
May contain references to other libraries in [this list](https://github.com/Indemos) that were NOT included in the current repository.

# Structure

* **Core** - cross-platform .NET Core class library with main functionality 
* **Chart** - [canvas](https://github.com/Indemos/Canvas) visualization
* **Estimator** - class [library](https://github.com/Indemos/Estimator) measuring performance metrics and statistics
* **Data** - catalog with historical data, any format is acceptable as long as you implement your own parser
* **Gateway** - gateway implementations for brokers and exchanges, including historical and simulated data
* **Terminal** - application that puts together orders, positions, performance metrics, and charts 
* **Derivative** - application visualizing data from option chains 

# Gateways 

* Schwab
* Alpaca
* Simulation - virtual orders and market data 

In order to create connector for preferred broker, implement interface `IGateway`.

# Trading Strategies

[Examples](https://github.com/Indemos/Terminal/tree/main/Terminal/Pages) of simple trading strategies can be found in `Terminal` catalog.

# Preview 

![](Screens/Preview.png)
