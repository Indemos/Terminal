# Stock Trading Terminal and Backtester - Web Version

All-in-one. Trading terminal with generic gateway implementation, tick backtester, charting, and performance evaluator for trading strategies.
Currently, supports only stocks with experimental extension for FX, options, and futures. 

# Disclaimer

The app is in active development state and can be updated without any notice. May contain references to other apps in [this list](https://github.com/Indemos) that were NOT included in the current repository.

# Structure

* **Core** - cross-platform .NET 5 class library that contains main functionality 
* **Chart** - graphics and [charts](https://github.com/Indemos/Canvas)
* **Client** - the main application that puts together windows for orders, positions, performance metrics, and charts 
* **Evaluation** - basic unit tests 
* **Score** - class library measuring performance and related statstics
* **Data** - catalog with historical data, any format is acceptable as long as you implement your own parser
* **Gateway** - gateway implementations for various brokers and exchanges, including historical and simulated data

# Gateways 

Both, trading and data gateways are included in the same interface. 
In order to create connector for preferable broker, implement interface `IConnectorModel`.

# Trading Strategies

[Examples](https://github.com/Indemos/Terminal/tree/main/Terminal.Client/Pages) of simple trading strategies can be found in `Client` catalog.

# Preview 

![](Screens/Preview.gif)
