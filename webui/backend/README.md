# WhackerLinkConsoleV2 Backend

This directory contains the backend API server for the WhackerLinkConsoleV2 web application.

## Backend: Node.js + Express

This is the complete implementation of the backend server using Node.js and Express.

### Installation

```bash
cd webui/backend
npm install
```

### Running the Server

```bash
npm start      # Start the server
npm run dev    # Start with nodemon for auto-reload during development
```

The server will start on `http://localhost:8080` by default.

## API Endpoints

### System Status
- `GET /api/health` - Health check endpoint
- `GET /api/status` - Get overall system status

### Codeplug Management
- `GET /api/codeplug` - Get current loaded codeplug
- `GET /api/codeplug/info` - Get codeplug information
- `GET /api/codeplug/systems` - Get systems list
- `GET /api/codeplug/zones` - Get zones with channels
- `GET /api/codeplug/channel/:channelName` - Get specific channel
- `POST /api/codeplug/load-yaml` - Load codeplug from YAML file
  - Body: `{ "filePath": "/path/to/codeplug.yml" }`

### PTT Control
- `POST /api/ptt` - Send PTT command
  - Body: `{ "channel": "channel-name", "system": "system-name", "action": "down|up" }`

### Page Commands
- `POST /api/page/p25` - Send P25 page
  - Body: `{ "system": "system-name", "dstId": "123456789" }`
- `POST /api/page/manual` - Send manual QC2 page
  - Body: `{ "toneA": "1750", "toneB": "2100" }`

### Alert Tones
- `GET /api/alerttones` - Get all alert tones
- `POST /api/alerttone/add` - Add new alert tone
  - Body: `{ "path": "/path/to/alert.wav" }`
- `DELETE /api/alerttone/:id` - Remove alert tone
- `POST /api/alerttone` - Send alert tone
  - Body: `{ "toneId": 1 }` (1, 2, or 3)

### Settings Management
- `GET /api/settings/v2` - Get current settings
- `POST /api/settings/v2` - Save settings (full)
  - Body: `{ "showSystemStatus": true, "showChannels": true, ... }`
- `PATCH /api/settings/:key` - Update individual setting
  - Body: `{ "value": true }`

### Widget Position Management
- `GET /api/positions/channels` - Get channel positions
- `POST /api/positions/channels` - Save channel position
  - Body: `{ "channelName": "name", "x": 100, "y": 200 }`
- `GET /api/positions/systems` - Get system status positions
- `POST /api/positions/systems` - Save system status position
  - Body: `{ "systemName": "name", "x": 100, "y": 200 }`
- `GET /api/positions/alerttones` - Get alert tone positions
- `POST /api/positions/alerttones` - Save alert tone position
  - Body: `{ "alertPath": "/path/to/alert.wav", "x": 100, "y": 200 }`

### System Status Management
- `GET /api/systems/status` - Get all system statuses
- `GET /api/systems/:systemName/status` - Get specific system status
- `POST /api/systems/:systemName/status` - Update system status
  - Body: `{ "state": "connected|disconnected|connecting", "connection": "ws://..." }`

### Audio Devices
- `GET /api/audio/devices` - Get available audio devices

### Call History
- `GET /api/callhistory` - Get call history (last 100 calls)
- `POST /api/callhistory` - Log new call
  - Body: `{ "channel": "channel-name", "srcId": "123456789", "dstId": "987654321" }`

### WebSocket Endpoints
- `GET /api/websocket/:systemName` - WebSocket connection for a system
  - Connection: `ws://localhost:8080/api/websocket/system-name`

## Features

### Settings Persistence
Settings are automatically saved to `UserSettings.json` in the parent directory:
- Show/hide system status
- Show/hide channels
- Show/hide alert tones
- Global PTT keybind
- Enable global PTT hotkey
- Hotkeys work when unfocused
- PTT sound settings (down/up)
- PTT tail delay
- Last codeplug path
- Channel positions
- System status positions
- Alert tone file paths
- Alert tone positions
- Channel output devices
- Channel stereo modes

### Codeplug Loading
The backend supports loading codeplug files from YAML format with:
- Automatic system status tracking
- Zone and channel parsing
- System status state tracking

### WebSocket Management
- Multiple system connections
- Automatic reconnection handling
- Connection state tracking
- Message routing to specific systems

## Requirements

### Node.js Dependencies
```
express - Web server framework
ws - WebSocket server
cors - Cross-origin resource sharing
yaml - YAML parser
body-parser - Request body parsing
```

## File Structure

```
webui/backend/
├── server.js          # Main server file
├── package.json       # Node.js dependencies
├── README.md          # This file
└── UserSettings.json  # Settings file (created automatically)