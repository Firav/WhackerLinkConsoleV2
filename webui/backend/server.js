/**
 * WhackerLinkConsoleV2 Backend Server
 * Node.js + Express Implementation
 * 
 * This backend handles:
 * - REST API endpoints for frontend communication
 * - WebSocket connections for real-time talkgroup communication
 * - Codeplug management (YAML parsing)
 * - Peer (WLink) and FNE (UDP) system management
 * - PTT, Page, Alert Tone control
 * - Call history tracking
 * - Settings management with persistence
 * - Audio device management
 * - Widget position persistence
 */

const express = require('express');
const http = require('http');
const WebSocket = require('ws');
const cors = require('cors');
const path = require('path');
const fs = require('fs');
const yaml = require('yaml');
const load = yaml.parse;
const crypto = require('crypto');
const os = require('os');

// Create Express app
const app = express();
const PORT = process.env.PORT || 8080;

// Middleware
app.use(cors());
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// In-memory stores
const connectedWebsockets = new Map();
const callHistory = [];
const alertTones = [
    { id: 1, path: 'Audio/alert1.wav' },
    { id: 2, path: 'Audio/alert2.wav' },
    { id: 3, path: 'Audio/alert3.wav' }
];
let codeplug = null;
const systemStatus = new Map();
const systemHandlers = new Map(); // Stores Peer/FNE system handlers

// Settings and Configuration
const SettingsManager = {
    SETTINGS_FILE: path.join(__dirname, '..', 'UserSettings.json'),
    
    defaultSettings: {
        showSystemStatus: true,
        showChannels: true,
        showAlertTones: true,
        globalPttKeybind: 'Ctrl+T',
        enableGlobalPttHotkey: true,
        hotkeysWorkWhenUnfocused: true,
        enablePttDownSound: false,
        enablePttUpSound: false,
        pttTailDelayMs: 0,
        lastCodeplugPath: null,
        channelPositions: {},
        systemStatusPositions: {},
        alertToneFilePaths: [],
        alertTonePositions: {},
        channelOutputDevices: {},
        channelStereoModes: {}
    },
    
    settings: null,
    
    load: function() {
        try {
            if (fs.existsSync(this.SETTINGS_FILE)) {
                const data = fs.readFileSync(this.SETTINGS_FILE, 'utf8');
                this.settings = { ...this.defaultSettings, ...JSON.parse(data) };
                console.log('Settings loaded from', this.SETTINGS_FILE);
            } else {
                this.settings = { ...this.defaultSettings };
            }
        } catch (err) {
            console.error('Error loading settings:', err.message);
            this.settings = { ...this.defaultSettings };
        }
    },
    
    save: function() {
        try {
            fs.writeFileSync(this.SETTINGS_FILE, JSON.stringify(this.settings, null, 2));
            console.log('Settings saved to', this.SETTINGS_FILE);
        } catch (err) {
            console.error('Error saving settings:', err.message);
        }
    },
    
    get: function(key) {
        return this.settings[key];
    },
    
    set: function(key, value) {
        this.settings[key] = value;
        this.save();
    },
    
    getAll: function() {
        return this.settings;
    }
};

// Codeplug position tracking
const positionTracker = {
    channelPositions: {},
    systemStatusPositions: {},
    alertTonePositions: {},
    
    saveChannelPosition: function(channelName, x, y) {
        this.channelPositions[channelName] = { x, y };
    },
    
    saveSystemStatusPosition: function(systemName, x, y) {
        this.systemStatusPositions[systemName] = { x, y };
    },
    
    saveAlertTonePosition: function(alertPath, x, y) {
        this.alertTonePositions[alertPath] = { x, y };
    }
};

// Serve static files (frontend)
app.use(express.static(path.join(__dirname, '../')));

// ================================================
// REST API Endpoints
// ================================================

// Root endpoint
app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, '../index.html'));
});

// Health check
app.get('/api/health', (req, res) => {
    res.json({ status: 'ok', timestamp: new Date().toISOString() });
});

// Get system status
app.get('/api/status', (req, res) => {
    res.json({
        codeplugLoaded: !!codeplug,
        systems: Array.from(systemStatus.keys()),
        connectedWebsockets: Array.from(connectedWebsockets.keys()),
        settings: SettingsManager.getAll()
    });
});

// Load codeplug
app.post('/api/codeplug/load', (req, res) => {
    // For now, just acknowledge the request
    // Full implementation would parse YAML and load codeplug data
    codeplug = {
        systems: req.body.systems || [],
        zones: req.body.zones || []
    };
    res.json({ success: true, message: 'Codeplug loaded successfully' });
});

// Get current codeplug
app.get('/api/codeplug', (req, res) => {
    if (!codeplug) {
        return res.status(404).json({ error: 'No codeplug loaded' });
    }
    res.json(codeplug);
});

// Load codeplug from YAML content
app.post('/api/codeplug/load-yaml', (req, res) => {
    const { yamlContent } = req.body;
    
    if (!yamlContent) {
        return res.status(400).json({ error: 'yamlContent is required' });
    }
    
    try {
        const codeplugData = load(yamlContent);
        
        // Validate required fields
        if (!codeplugData.systems || !codeplugData.zones) {
            return res.status(400).json({ error: 'Invalid codeplug format: missing systems or zones' });
        }
        
        codeplug = codeplugData;
        
        // Clear and rebuild system status
        systemStatus.clear();
        codeplug.systems.forEach(system => {
            systemStatus.set(system.name, {
                name: system.name,
                address: system.address,
                port: system.port,
                state: 'disconnected'
            });
        });
        
        console.log(`Codeplug loaded: ${codeplug.systems.length} systems, ${codeplug.zones.length} zones, ${codeplug.zones.reduce((acc, z) => acc + z.channels.length, 0)} channels`);
        
        res.json({ 
            success: true, 
            message: 'Codeplug loaded successfully',
            systems: codeplug.systems.length,
            zones: codeplug.zones.length,
            totalChannels: codeplug.zones.reduce((acc, z) => acc + z.channels.length, 0)
        });
    } catch (err) {
        console.error('Error loading codeplug:', err.message);
        res.status(500).json({ error: 'Failed to load codeplug: ' + err.message });
    }
});

// Get codeplug file info
app.get('/api/codeplug/info', (req, res) => {
    if (!codeplug) {
        return res.status(404).json({ error: 'No codeplug loaded' });
    }
    
    res.json({
        lastCodeplugPath: SettingsManager.get('lastCodeplugPath'),
        systems: codeplug.systems,
        zones: codeplug.zones,
        totalChannels: codeplug.zones.reduce((acc, z) => acc + z.channels.length, 0)
    });
});

// Get systems list
app.get('/api/codeplug/systems', (req, res) => {
    if (!codeplug) {
        return res.status(404).json({ error: 'No codeplug loaded' });
    }
    res.json({ systems: codeplug.systems });
});

// Get zones with channels
app.get('/api/codeplug/zones', (req, res) => {
    if (!codeplug) {
        return res.status(404).json({ error: 'No codeplug loaded' });
    }
    res.json({ zones: codeplug.zones });
});

// Get specific channel
app.get('/api/codeplug/channel/:channelName', (req, res) => {
    if (!codeplug) {
        return res.status(404).json({ error: 'No codeplug loaded' });
    }
    
    const { channelName } = req.params;
    const channel = codeplug.zones
        .flatMap(z => z.channels)
        .find(c => c.name === channelName);
    
    if (!channel) {
        return res.status(404).json({ error: 'Channel not found' });
    }
    
    res.json({ channel });
});

// PTT Control
app.post('/api/ptt', (req, res) => {
    const { channel, system, action } = req.body;
    
    if (!channel || !system || !action) {
        return res.status(400).json({ error: 'channel, system, and action are required' });
    }
    
    // Forward to WebSocket connection if available
    if (connectedWebsockets.has(system)) {
        const ws = connectedWebsockets.get(system);
        if (ws.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify({ type: 'PTT', channel, system, action }));
        }
    }
    
    console.log(`PTT ${action} for ${channel} on ${system}`);
    res.json({ success: true, message: `PTT ${action} sent` });
});

// P25 Page Command
app.post('/api/page/p25', (req, res) => {
    const { system, dstId } = req.body;
    
    if (!system || !dstId) {
        return res.status(400).json({ error: 'system and dstId are required' });
    }
    
    if (connectedWebsockets.has(system)) {
        const ws = connectedWebsockets.get(system);
        if (ws.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify({ type: 'P25_PAGE', system, dstId }));
        }
    }
    
    console.log(`P25 Page to ${dstId} on ${system}`);
    res.json({ success: true, message: `P25 page sent to ${dstId}` });
});

// Manual QC2 Page Command
app.post('/api/page/manual', (req, res) => {
    const { toneA, toneB } = req.body;
    
    if (!toneA || !toneB) {
        return res.status(400).json({ error: 'toneA and toneB are required' });
    }
    
    console.log(`Manual QC2 page: A=${toneA}, B=${toneB}`);
    res.json({ success: true, message: `Manual page sent: A=${toneA}, B=${toneB}` });
});

// Alert Tone Command
app.post('/api/alerttone', (req, res) => {
    const { toneId } = req.body;
    
    if (!toneId || ![1, 2, 3].includes(toneId)) {
        return res.status(400).json({ error: 'toneId must be 1, 2, or 3' });
    }
    
    const alertTone = alertTones.find(t => t.id === toneId);
    console.log(`Alert tone ${toneId} sent: ${alertTone.path}`);
    res.json({ success: true, message: `Alert tone ${toneId} sent`, path: alertTone.path });
});

// Log call to history
app.post('/api/callhistory', (req, res) => {
    const { channel, srcId, dstId } = req.body;
    
    const call = {
        id: Date.now(),
        channel,
        srcId,
        dstId,
        timestamp: new Date().toISOString()
    };
    
    callHistory.unshift(call);
    if (callHistory.length > 100) {
        callHistory.pop();
    }
    
    res.json({ success: true, historyLength: callHistory.length });
});

// Get call history
app.get('/api/callhistory', (req, res) => {
    res.json({ calls: callHistory });
});

// Get available audio devices
app.get('/api/audio/devices', (req, res) => {
    // This would query the system for actual devices
    res.json({
        input: [
            { id: 'default', name: 'Default Input' },
            { id: 'microphone', name: 'Microphone Array' }
        ],
        output: [
            { id: 'default', name: 'Default Output' },
            { id: 'headphones', name: 'Headphones' }
        ]
    });
});

// Get settings (deprecated, use /api/settings/v2)
app.get('/api/settings', (req, res) => {
    res.json(settings);
});

// Get settings using SettingsManager (preferred)
app.get('/api/settings/v2', (req, res) => {
    res.json(SettingsManager.getAll());
});

// Save settings using SettingsManager (preferred)
app.post('/api/settings/v2', (req, res) => {
    const newSettings = req.body;
    let changed = false;
    
    for (const key in newSettings) {
        if (SettingsManager.defaultSettings.hasOwnProperty(key)) {
            if (SettingsManager.settings[key] !== newSettings[key]) {
                SettingsManager.settings[key] = newSettings[key];
                changed = true;
            }
        }
    }
    
    if (changed) {
        SettingsManager.save();
    }
    
    res.json({ success: true, settings: SettingsManager.getAll() });
});

// Update individual setting
app.patch('/api/settings/:key', (req, res) => {
    const { key } = req.params;
    const { value } = req.body;
    
    if (!SettingsManager.defaultSettings.hasOwnProperty(key)) {
        return res.status(400).json({ error: `Invalid setting key: ${key}` });
    }
    
    SettingsManager.settings[key] = value;
    SettingsManager.save();
    
    res.json({ success: true, settings: SettingsManager.getAll() });
});

// Save settings
app.post('/api/settings', (req, res) => {
    for (const key in req.body) {
        if (settings.hasOwnProperty(key)) {
            settings[key] = req.body[key];
        }
    }
    res.json({ success: true, settings });
});

// Get widget status
app.get('/api/widget/status', (req, res) => {
    res.json({
        systems: Array.from(systemStatus.keys()),
        channels: codeplug?.zones || [],
        alertTones: alertTones
    });
});

// Get channel positions
app.get('/api/positions/channels', (req, res) => {
    res.json({ positions: positionTracker.channelPositions });
});

// Save channel position
app.post('/api/positions/channels', (req, res) => {
    const { channelName, x, y } = req.body;
    
    if (!channelName || x === undefined || y === undefined) {
        return res.status(400).json({ error: 'channelName, x, and y are required' });
    }
    
    positionTracker.saveChannelPosition(channelName, x, y);
    SettingsManager.set('channelPositions', positionTracker.channelPositions);
    
    res.json({ success: true, positions: positionTracker.channelPositions });
});

// Get system status positions
app.get('/api/positions/systems', (req, res) => {
    res.json({ positions: positionTracker.systemStatusPositions });
});

// Save system status position
app.post('/api/positions/systems', (req, res) => {
    const { systemName, x, y } = req.body;
    
    if (!systemName || x === undefined || y === undefined) {
        return res.status(400).json({ error: 'systemName, x, and y are required' });
    }
    
    positionTracker.saveSystemStatusPosition(systemName, x, y);
    SettingsManager.set('systemStatusPositions', positionTracker.systemStatusPositions);
    
    res.json({ success: true, positions: positionTracker.systemStatusPositions });
});

// Get alert tone positions
app.get('/api/positions/alerttones', (req, res) => {
    res.json({ positions: positionTracker.alertTonePositions });
});

// Save alert tone position
app.post('/api/positions/alerttones', (req, res) => {
    const { alertPath, x, y } = req.body;
    
    if (!alertPath || x === undefined || y === undefined) {
        return res.status(400).json({ error: 'alertPath, x, and y are required' });
    }
    
    positionTracker.saveAlertTonePosition(alertPath, x, y);
    SettingsManager.set('alertTonePositions', positionTracker.alertTonePositions);
    
    res.json({ success: true, positions: positionTracker.alertTonePositions });
});

// Update system status
app.post('/api/systems/:systemName/status', (req, res) => {
    const { systemName } = req.params;
    const { state, connection, ...extraData } = req.body;
    
    const systemData = {
        name: systemName,
        state: state || 'unknown',
        connection: connection || null,
        lastUpdate: new Date().toISOString(),
        ...extraData
    };
    
    systemStatus.set(systemName, systemData);
    
    res.json({ success: true, system: systemData });
});

// Get specific system status
app.get('/api/systems/:systemName/status', (req, res) => {
    const { systemName } = req.params;
    
    if (!systemStatus.has(systemName)) {
        return res.status(404).json({ error: `System '${systemName}' not found` });
    }
    
    res.json({ status: systemStatus.get(systemName) });
});

// Get all system statuses
app.get('/api/systems/status', (req, res) => {
    const statuses = {};
    systemStatus.forEach((value, key) => {
        statuses[key] = value;
    });
    res.json({ systems: statuses });
});

// Add alert tone
app.post('/api/alerttone/add', (req, res) => {
    const { path } = req.body;
    
    if (!path) {
        return res.status(400).json({ error: 'path is required' });
    }
    
    // Check if already exists
    const exists = alertTones.find(t => t.path === path);
    if (exists) {
        return res.status(400).json({ error: 'Alert tone already exists' });
    }
    
    const newId = alertTones.length + 1;
    const newTone = { id: newId, path };
    
    alertTones.push(newTone);
    SettingsManager.set('alertToneFilePaths', alertTones.map(t => t.path));
    
    res.json({ success: true, alertTone: newTone, alertTones });
});

// Remove alert tone
app.delete('/api/alerttone/:id', (req, res) => {
    const { id } = req.params;
    
    const index = alertTones.findIndex(t => t.id === parseInt(id));
    if (index === -1) {
        return res.status(404).json({ error: 'Alert tone not found' });
    }
    
    alertTones.splice(index, 1);
    
    // Reassign IDs
    alertTones.forEach((t, i) => { t.id = i + 1; });
    
    SettingsManager.set('alertToneFilePaths', alertTones.map(t => t.path));
    
    res.json({ success: true, alertTones });
});

// Get alert tones
app.get('/api/alerttones', (req, res) => {
    res.json({ alertTones });
});

// ================================================
// System Handlers Management (for Peer and FNE systems)
// ================================================

/**
 * Add a WebSocket handler for a system (Peer/WLink)
 */
function addWebSocketHandler(systemName, ws) {
    if (connectedWebsockets.has(systemName)) {
        connectedWebsockets.get(systemName).close();
    }
    connectedWebsockets.set(systemName, ws);
    console.log(`WebSocket handler added for system: ${systemName}`);
}

/**
 * Get a WebSocket handler by system name
 */
function getWebSocketHandler(systemName) {
    return connectedWebsockets.get(systemName);
}

/**
 * Remove a WebSocket handler by system name
 */
function removeWebSocketHandler(systemName) {
    if (connectedWebsockets.has(systemName)) {
        connectedWebsockets.delete(systemName);
        console.log(`WebSocket handler removed for system: ${systemName}`);
    }
}

/**
 * Get all connected WebSocket handlers
 */
function getAllWebSocketHandlers() {
    return Array.from(connectedWebsockets.keys());
}

/**
 * Check if a system has an active connection
 */
function hasWebSocketConnection(systemName) {
    return connectedWebsockets.has(systemName);
}

/**
 * Send a message to a specific system
 */
function sendToSystem(systemName, message) {
    const ws = connectedWebsockets.get(systemName);
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify(message));
        return true;
    }
    return false;
}

// ================================================
// WebSocket Endpoints
// ================================================

const server = http.createServer(app);
const wss = new WebSocket.Server({ server });

wss.on('connection', (ws, req) => {
    // Parse system name from URL
    const url = new URL(req.url, `ws://localhost:${PORT}`);
    const pathName = url.pathname;
    
    // Extract system name from path: /api/websocket/:systemName
    let systemName = null;
    const pathParts = pathName.split('/').filter(p => p);
    if (pathParts.length > 2 && pathParts[0] === 'api' && pathParts[1] === 'websocket') {
        systemName = pathParts[2];
    }
    
    if (!systemName) {
        console.log(`Unknown connection attempt: ${pathName}`);
        ws.close();
        return;
    }
    
    console.log(`WebSocket connected: ${systemName}`);
    addWebSocketHandler(systemName, ws);
    
    // Handle messages
    ws.on('message', (message) => {
        try {
            const data = JSON.parse(message);
            handleWebSocketMessage(ws, systemName, data);
        } catch (err) {
            console.error('Invalid JSON received:', message);
        }
    });
    
    ws.on('close', () => {
        console.log(`WebSocket disconnected: ${systemName}`);
        removeWebSocketHandler(systemName);
    });
    
    ws.on('error', (err) => {
        console.error(`WebSocket error for ${systemName}:`, err);
        removeWebSocketHandler(systemName);
    });
    
    // Send welcome message
    ws.send(JSON.stringify({ type: 'connected', system: systemName }));
});

function handleWebSocketMessage(ws, systemName, message) {
    const { type } = message;
    
    switch (type) {
        case 'PTT':
            console.log(`PTT ${message.action} for ${message.channel} on ${systemName}`);
            ws.send(JSON.stringify({ status: 'acknowledged' }));
            break;
        
        case 'P25_PAGE':
            console.log(`P25 Page to ${message.dstId} on ${systemName}`);
            ws.send(JSON.stringify({ status: 'acknowledged' }));
            break;
        
        case 'register':
            console.log(`Client registered on ${systemName}`);
            ws.send(JSON.stringify({ status: 'registered', system: systemName }));
            break;
        
        case 'audio_data':
            // Handle incoming audio data
            console.log(`Received audio data from ${systemName}`);
            // Forward to other connected clients if needed
            break;
        
        case 'call_alert':
            // Handle incoming call alert
            console.log(`Call alert from ${systemName}: ${JSON.stringify(message)}`);
            break;
        
        case 'emergency':
            // Handle emergency alarm
            console.log(`Emergency alarm from ${systemName}`);
            break;
        
        default:
            console.log(`Unknown message type: ${type}`);
    }
}

// ================================================
// Server Startup
// ================================================

server.listen(PORT, () => {
    console.log(`WhackerLinkConsoleV2 Backend Server`);
    console.log(`Server running on http://localhost:${PORT}`);
    console.log(`WebSocket endpoint: ws://localhost:${PORT}/api/websocket/:systemName`);
    console.log(`Frontend available at: http://localhost:${PORT}`);
});

module.exports = {
    app,
    
    // System Handlers Management
    addWebSocketHandler,
    getWebSocketHandler,
    removeWebSocketHandler,
    getAllWebSocketHandlers,
    hasWebSocketConnection,
    sendToSystem,
    
    // Settings Management
    SettingsManager,
    
    // Codeplug Management
    codeplug,
    systemStatus,
    connectedWebsockets,
    callHistory,
    alertTones,
    systemHandlers,
    
    // Initialize settings on module load
    init: function() {
        SettingsManager.load();
        console.log('Backend initialized');
    }
};
