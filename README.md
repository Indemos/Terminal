# Trading Terminal and Backtester

All-in-one. 
Trading terminal with generic gateway implementation, tick backtester, charting, and performance evaluator for trading strategies.
Supports stocks, FX, options, and futures with experimental support for crypto-currencies. 
May contain references to other libraries in [this list](https://github.com/Indemos) that were not included in this repository.

# Status 

![GitHub Workflow Status (with event)](https://img.shields.io/github/actions/workflow/status/Indemos/Terminal/dotnet.yml?event=push)
![GitHub](https://img.shields.io/github/license/Indemos/Terminal)
![GitHub](https://img.shields.io/badge/system-Windows%20%7C%20Linux%20%7C%20Mac-blue)

# Structure

* **Core** - cross-platform .NET Core library with main functionality 
* **Data** - historical ticks for backtester in ZIP, JSON or Message Pack format + custom parsers
* **Gateways** - gateway implementations for brokers and exchanges, including simulated data
* **Dashboard** - UI for strategies visualizing orders, positions, and performance metrics 

# Gateways 

* Interactive Brokers
* Simulation - virtual orders and market data 

In order to create connector for preferred broker, implement interface `IGateway`.

# Migration

The application is being migrated to Orleans to simplify maintenance and tracking. 
Gateways below are still being migrated. 
Previous implementation with already implemented gateways is available in the `gateways` branch.

* Tradier
* Schwab
* Alpaca

# Trading Strategies

[Examples](https://github.com/Indemos/Terminal/tree/main/Terminal/Pages) of simple trading strategies can be found in `Terminal` catalog.

# Preview 

![](Screens/Preview.png)

# Notes

