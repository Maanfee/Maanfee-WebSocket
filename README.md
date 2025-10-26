# Maanfee.WebSocket Library

A comprehensive WebSocket library for .NET providing both client and server implementations with support for multiple application types.

## 📦 Packages

### Maanfee.WebSocket
Core WebSocket library containing:
- `WebSocketServer` - Full-featured WebSocket server
- `WebSocketClient` - WebSocket client implementation
- Event arguments and helper classes

### Maanfee.Examples
Example implementations demonstrating usage in different application types.

# 🛠️ API Reference

## WebSocketServer Methods

| Method | Description |
|--------|-------------|
| `Start()` | Start the server |
| `StopAsync()` | Stop the server gracefully |
| `HandleWebSocketConnection(WebSocket)` | Handle new WebSocket connections |
| `BroadcastMessage(string)` | Send message to all connected clients |
| `SendToClientAsync(string clientId, string message)` | Send message to specific client |
| `GetConnectedClientsCount()` | Get number of connected clients |
| `Dispose()` | Clean up resources |

## WebSocketClient Methods

| Method | Description |
|--------|-------------|
| `ConnectAsync()` | Connect to WebSocket server |
| `SendMessageAsync(string)` | Send message to server |
| `DisconnectAsync()` | Disconnect from server |
| `Dispose()` | Clean up resources |

# 🚀 Quick Start
# Solution Configuration Guide

## Steps to Configure Multiple Startup Projects

1. **Open the Solution**
   - Right-click on the solution name in Solution Explorer

2. **Access Solution Properties**
   - Go to `Properties`

3. **Configure Startup Projects**
   - Navigate to `Multiple startup projects`

## Available Project Profiles

### Web API Server Profile
- **Server**: Web API Server
- **Clients**: 
  - Blazor
  - Web API 
  - Console

### Console Server Profile  
- **Server**: Console Server
- **Clients**:
  - Blazor
  - Web API
  - Console

## Configuration Options
Each profile allows you to set different combinations of server and client applications for testing and development purposes.

<div style="display: flex; gap: 20px; flex-wrap: wrap; justify-content: center;">
    <div style="text-align: center;">
        <img src="SolutionItems/Screenshots/1.png" style="width: 200px; height: 150px; object-fit: cover; border-radius: 8px;" />
    </div> 
    <div style="text-align: center;"> 
        <img src="SolutionItems/Screenshots/2.png" style="width: 200px; height: 150px; object-fit: cover; border-radius: 8px;" /> 
    </div>
</div>
