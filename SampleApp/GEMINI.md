# SampleApp Context

## Purpose
The primary reference implementation for consumers of the library. It demonstrates "Best Practices".

## Features Covered
1.  **Transport Configuration**: Using `TransportBuilder` with all options (Security, Reliability, Ordering).
2.  **Message Handling**: Defining handlers for specific message types.
3.  **App Settings**: Loading config from `appsettings.json`.
4.  **Interactive Mode**: CLI arguments to toggle features (Encryption on/off, ACK requests).

## Dependencies
*   `Ubicomp.Utils.NET.MulticastTransportFramework`
*   `Microsoft.Extensions.Hosting` (or similar for generic host/config patterns)

## Do's and Don'ts
*   **Do** reference this app for canonical `TransportBuilder` usage.
*   **Do** use the interactive command-line arguments to test different configurations (e.g., encryption toggles).
*   **Don't** commit secrets or keys used in this app to version control.
