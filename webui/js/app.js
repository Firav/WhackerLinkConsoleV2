/**
 * WhackerLinkConsoleV2 - Web Edition
 * Vanilla JavaScript Application
 * 
 * This file contains all the application logic for the WhackerLinkConsoleV2 web interface
 */

/* ================================================
   Application State
   ================================================ */
const AppState = {
    codeplug: null,
    systems: [],
    channels: [],
    settings: {
        showSystemStatus: true,
        showChannels: true,
        showAlertTones: true,
        lastCodeplugPath: null,
        channelPositions: {},
        systemStatusPositions: {},
        alertToneFilePaths: [],
        alertTonePositions: {},
        channelOutputDevices: {},
        channelStereoModes: {},
        globalPttKeybind: "Ctrl+T",
        enableGlobalPttHotkey: true,
        hotkeysWorkWhenUnfocused: true,
        enablePttDownSound: false,
        enablePttUpSound: false,
        pttTailDelayMs: 0
    },
    isEditMode: false,
    globalPttState: false,
    dialogs: {},
    widgets: []
};

/* ================================================
   DOM Elements Cache
   ================================================ */
const DOM = {
    app: document.getElementById('app'),
    channelsCanvas: document.getElementById('channels-canvas'),
    fileInputCodeplug: document.getElementById('file-input-codeplug'),
    fileInputAlertTone: document.getElementById('file-input-alert-tone'),
    
    // Menu items
    menuLabels: document.querySelectorAll('.menu-label'),
    
    // Toolbar buttons
    btnAlert1: document.getElementById('btnAlert1'),
    btnAlert2: document.getElementById('btnAlert2'),
    btnAlert3: document.getElementById('btnAlert3'),
    btnGlobalPtt: document.getElementById('btnGlobalPtt'),
    btnClearEmergency: document.getElementById('btnClearEmergency'),
    btnOpenCodeplug: document.getElementById('btnOpenCodeplug'),
    btnP25Page: document.getElementById('btnP25Page'),
    btnSelectAll: document.getElementById('btnSelectAll'),
    btnKeyStats: document.getElementById('btnKeyStats'),
    btnCallHistory: document.getElementById('btnCallHistory'),
    btnAudioSettings: document.getElementById('btnAudioSettings'),
    
    // Dialogs
    dialogs: {
        audioSettings: document.getElementById('audio-settings-dialog'),
        digitalPage: document.getElementById('digital-page-dialog'),
        manualPage: document.getElementById('manual-page-dialog'),
        keyStatus: document.getElementById('key-status-dialog'),
        callHistory: document.getElementById('call-history-dialog'),
        widgetSelection: document.getElementById('widget-selection-dialog'),
        keybindingConfig: document.getElementById('keybinding-config-dialog')
    },
    
    // Audio Settings
    inputDeviceSelect: document.getElementById('input-device-select'),
    enablePttDownSound: document.getElementById('enable-ptt-down-sound'),
    enablePttUpSound: document.getElementById('enable-ptt-up-sound'),
    pttTailDelay: document.getElementById('ptt-tail-delay'),
    channelOutputDevices: document.getElementById('channel-output-devices'),
    audioSettingsSave: document.getElementById('audio-settings-save'),
    audioSettingsCancel: document.getElementById('audio-settings-cancel'),
    
    // Digital Page
    pageSystemSelect: document.getElementById('page-system-select'),
    pageDstId: document.getElementById('page-dst-id'),
    pageSendBtn: document.getElementById('page-send-btn'),
    
    // Manual Page
    manualToneA: document.getElementById('manual-tone-a'),
    manualToneB: document.getElementById('manual-tone-b'),
    manualPageSendBtn: document.getElementById('manual-page-send-btn'),
    
    // Key Status
    keyStatusTable: document.getElementById('key-status-table'),
    
    // Call History
    callHistoryTable: document.getElementById('call-history-table'),
    
    // Widget Selection
    showSystemStatus: document.getElementById('show-system-status'),
    showChannels: document.getElementById('show-channels'),
    showAlertTones: document.getElementById('show-alert-tones'),
    widgetSelectionApply: document.getElementById('widget-selection-apply'),
    
    // Keybinding Config
    globalPttDisplay: document.getElementById('global-ptt-display'),
    recordGlobalPttBtn: document.getElementById('record-global-ptt-btn'),
    clearGlobalPttBtn: document.getElementById('clear-global-ptt-btn'),
    hotkeysUnfocused: document.getElementById('hotkeys-unfocused'),
    keybindingApply: document.getElementById('keybinding-apply'),
    keybindingClose: document.getElementById('keybinding-close'),
    
    // Tabs
    tabBtns: document.querySelectorAll('.tab-btn'),
    pttKeybindingsContent: document.getElementById('ptt-keybindings-content'),
    channelToggleContent: document.getElementById('channel-toggle-content')
};

/* ================================================
   Utility Functions
   ================================================ */
const Utils = {
    // Generate a unique ID
    generateId: () => 'id-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9),
    
    // Load settings from localStorage
    loadSettings: () => {
        const saved = localStorage.getItem('whackerlink_settings');
        if (saved) {
            try {
                AppState.settings = JSON.parse(saved);
            } catch (e) {
                console.error('Failed to load settings:', e);
            }
        }
    },
    
    // Save settings to localStorage
    saveSettings: () => {
        localStorage.setItem('whackerlink_settings', JSON.stringify(AppState.settings));
    },
    
    // Load codeplug from file via backend API
    loadCodeplug: async (file) => {
        const reader = new FileReader();
        return new Promise((resolve, reject) => {
            reader.onload = async (e) => {
                try {
                    const yamlContent = e.target.result;
                    
                    // Pass YAML content to backend
                    const response = await fetch('/api/codeplug/load-yaml', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify({
                            yamlContent: yamlContent
                        })
                    });
                    
                    const data = await response.json();
                    
                    if (response.ok) {
                        // Reload the codeplug from the backend to get the full data
                        const codeplugResponse = await fetch('/api/codeplug');
                        if (codeplugResponse.ok) {
                            AppState.codeplug = await codeplugResponse.json();
                            AppState.settings.lastCodeplugPath = file.name;
                            Utils.saveSettings();
                            resolve(AppState.codeplug);
                        } else {
                            resolve({ systems: [], zones: [] });
                        }
                    } else {
                        reject(new Error(data.error || 'Failed to load codeplug'));
                    }
                } catch (err) {
                    reject(err);
                }
            };
            reader.onerror = () => reject(reader.error);
            reader.readAsText(file);
        });
    },
    
    // Simple YAML parser
    parseYaml: (yaml) => {
        // This is a simplified YAML parser
        // For production, you'd want to use a proper YAML library like js-yaml
        const lines = yaml.split('\n');
        const result = { systems: [], zones: [] };
        let currentSection = null;
        let currentSystem = null;
        let currentZone = null;
        
        for (const line of lines) {
            if (!line.trim() || line.trim().startsWith('#')) continue;
            
            const indent = line.match(/^\s*/)[0].length;
            const content = line.trim();
            
            if (content === 'systems:') {
                currentSection = 'systems';
            } else if (content === 'zones:') {
                currentSection = 'zones';
            } else if (content.startsWith('- name:')) {
                if (currentSection === 'systems') {
                    currentSystem = { name: content.split(':')[1].trim() };
                    result.systems.push(currentSystem);
                } else if (currentSection === 'zones') {
                    currentZone = { name: content.split(':')[1].trim(), channels: [] };
                    result.zones.push(currentZone);
                }
            } else if (currentSystem && currentSection === 'systems') {
                const [key, value] = content.split(':').map(s => s.trim());
                if (key) currentSystem[key] = value;
            } else if (currentZone && currentSection === 'zones') {
                if (content.startsWith('channels:')) {
                    // Skip, we'll handle channels below
                } else if (content.startsWith('- name:')) {
                    const channel = { name: content.split(':')[1].trim() };
                    currentZone.channels.push(channel);
                } else if (currentZone.channels.length > 0) {
                    const [key, value] = content.split(':').map(s => s.trim());
                    if (key) currentZone.channels[currentZone.channels.length - 1][key] = value;
                }
            }
        }
        
        return result;
    },
    
    // Copy to clipboard
    copyToClipboard: (text) => {
        navigator.clipboard.writeText(text).catch(err => console.error('Clipboard error:', err));
    },
    
    // Debounce function
    debounce: (func, wait) => {
        let timeout;
        return (...args) => {
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(this, args), wait);
        };
    },
    
    // Play alert tone from URL
    playAlertTone: (url) => {
        const audio = new Audio(url);
        audio.volume = 1.0;
        return audio.play().catch(err => console.error('Play alert tone error:', err));
    }
};

/* ================================================
   Audio Manager
   ================================================ */
class AudioManager {
    constructor() {
        this.context = null;
        this.audioElements = {};
        this.inputDevices = [];
        this.outputDevices = [];
    }
    
    async init() {
        try {
            this.context = new (window.AudioContext || window.webkitAudioContext)();
            await this.loadDevices();
        } catch (err) {
            console.error('Failed to init audio:', err);
        }
    }
    
    async loadDevices() {
        try {
            await navigator.mediaDevices.enumerateDevices();
            const devices = await navigator.mediaDevices.enumerateDevices();
            
            this.inputDevices = devices.filter(d => d.kind === 'audioinput');
            this.outputDevices = devices.filter(d => d.kind === 'audiooutput');
            
            return {
                input: this.inputDevices,
                output: this.outputDevices
            };
        } catch (err) {
            console.error('Failed to load audio devices:', err);
            return { input: [], output: [] };
        }
    }
    
    async getInputStream() {
        try {
            return await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
        } catch (err) {
            console.error('Failed to get audio input:', err);
            return null;
        }
    }
    
    playTone(toneType, frequency = 440) {
        if (!this.context) return;
        
        const oscillator = this.context.createOscillator();
        const gain = this.context.createGain();
        
        oscillator.type = 'sine';
        oscillator.frequency.value = frequency;
        gain.gain.value = 0.1;
        
        oscillator.connect(gain);
        gain.connect(this.context.destination);
        
        oscillator.start();
        oscillator.stop(this.context.currentTime + 0.1);
    }
    
    playAlertTone(url) {
        const audio = new Audio(url);
        audio.volume = 1.0;
        return audio.play().catch(err => console.error('Play alert tone error:', err));
    }
    
    addTalkgroupStream(talkgroupId, audioData) {
        // Create audio element for talkgroup stream
        if (!this.audioElements[talkgroupId]) {
            const audio = new Audio();
            audio.src = URL.createObjectURL(new Blob([audioData], { type: 'audio/wav' }));
            audio.loop = false;
            audio.volume = 1.0;
            this.audioElements[talkgroupId] = audio;
        }
        
        this.audioElements[talkgroupId].play().catch(err => console.error('Play stream error:', err));
    }
    
    setTalkgroupVolume(talkgroupId, volume) {
        if (this.audioElements[talkgroupId]) {
            this.audioElements[talkgroupId].volume = volume;
        }
    }
    
    stop() {
        Object.values(this.audioElements).forEach(audio => audio.pause());
    }
}

/* ================================================
   WebSocket Manager
   ================================================ */
class WebSocketManager {
    constructor() {
        this.webSockets = {};
    }
    
    addWebSocketHandler(systemName) {
        // Create WebSocket connection
        const ws = new WebSocket(`ws://localhost:8080/api/websocket/${systemName}`);
        
        ws.onopen = () => {
            console.log(`Connected to ${systemName}`);
            // Send registration request
            this.sendRegistration(systemName);
        };
        
        ws.onmessage = (event) => {
            this.handleMessage(systemName, event.data);
        };
        
        ws.onclose = () => {
            console.log(`Disconnected from ${systemName}`);
        };
        
        ws.onerror = (err) => {
            console.error(`WebSocket error for ${systemName}:`, err);
        };
        
        this.webSockets[systemName] = ws;
        return ws;
    }
    
    getWebSocketHandler(systemName) {
        return this.webSockets[systemName];
    }
    
    sendRegistration(systemName) {
        const ws = this.webSockets[systemName];
        if (ws && ws.readyState === WebSocket.OPEN) {
            const msg = JSON.stringify({
                type: 'registration',
                data: { system: systemName }
            });
            ws.send(msg);
        }
    }
    
    sendMessage(systemName, message) {
        const ws = this.webSockets[systemName];
        if (ws && ws.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify(message));
        }
    }
    
    sendMessageRaw(systemName, data) {
        const ws = this.webSockets[systemName];
        if (ws && ws.readyState === WebSocket.OPEN) {
            ws.send(data);
        }
    }
    
    handleP25Message(systemName, data) {
        // Parse P25 message
        console.log('P25 Message received:', data);
    }
    
    clearAllWebSocketHandlers() {
        Object.values(this.webSockets).forEach(ws => {
            if (ws.readyState === WebSocket.OPEN) {
                ws.close();
            }
        });
        this.webSockets = {};
    }
}

/* ================================================
   Widget Factory - Channel Widget
   ================================================ */
class ChannelWidget {
    constructor(channelData, audioManager, websocketManager) {
        this.channelData = channelData;
        this.audioManager = audioManager;
        this.websocketManager = websocketManager;
        this.element = this.create();
        this.isPTT = false;
        this.isPage = false;
        this.isHold = false;
        this.isEmergency = false;
        this.isReceiving = false;
        this.volume = 1.0;
        this.systemName = channelData.system;
    }
    
    create() {
        const container = document.createElement('div');
        container.className = 'channel-widget';
        container.dataset.channelName = this.channelData.name;
        
        container.innerHTML = `
            <button class="ptt-button" title="PTT">
                <img src="assets/instantptt.png" alt="PTT">
            </button>
            <div class="channel-widget-content-inner">
                <div class="channel-header">
                    <div class="channel-name">${this.channelData.name}</div>
                    <div class="last-src-id">Last SRC: -</div>
                    <div class="system-name">System: ${this.channelData.system}</div>
                </div>
                <div class="volume-control">
                    <input type="range" min="0" max="4" step="0.25" value="1" class="volume-slider">
                </div>
                <div class="channel-actions">
                    <button class="page-select-btn" title="Page Select">
                        <img src="assets/pageselect.png" alt="Page">
                    </button>
                    <button class="channel-marker-btn" title="Channel Hold">
                        <img src="assets/channelmarker.png" alt="Hold">
                    </button>
                </div>
            </div>
        `;
        
        // Store references to child elements
        this.pttButton = container.querySelector('.ptt-button');
        this.pageSelectBtn = container.querySelector('.page-select-btn');
        this.channelMarkerBtn = container.querySelector('.channel-marker-btn');
        this.volumeSlider = container.querySelector('.volume-slider');
        this.lastSrcId = container.querySelector('.last-src-id');
        
        // Add event listeners
        this.addEventListeners(container);
        
        return container;
    }
    
    addEventListeners(container) {
        // PTT button click
        this.pttButton.addEventListener('click', (e) => {
            e.stopPropagation();
            this.togglePTT();
        });
        
        // Page button click
        this.pageSelectBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.togglePage();
        });
        
        // Channel marker click
        this.channelMarkerBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleHold();
        });
        
        // Volume change
        this.volumeSlider.addEventListener('input', (e) => {
            this.volume = parseFloat(e.target.value);
            this.audioManager.setTalkgroupVolume(this.channelData.name, this.volume);
        });
        
        // Click to select/deselect
        container.addEventListener('click', (e) => {
            if (e.target !== this.pttButton && 
                e.target !== this.pageSelectBtn && 
                e.target !== this.channelMarkerBtn) {
                this.toggleSelection(container);
            }
        });
    }
    
    togglePTT() {
        this.isPTT = !this.isPTT;
        
        if (this.isPTT) {
            this.pttButton.classList.add('active');
            this.sendPTTDown();
        } else {
            this.pttButton.classList.remove('active');
            this.sendPTTUp();
        }
    }
    
    togglePage() {
        this.isPage = !this.isPage;
        
        if (this.isPage) {
            this.pageSelectBtn.classList.add('active');
            this.sendPageDown();
        } else {
            this.pageSelectBtn.classList.remove('active');
            this.sendPageUp();
        }
    }
    
    toggleHold() {
        this.isHold = !this.isHold;
        
        if (this.isHold) {
            this.channelMarkerBtn.classList.add('active');
            this.sendHold();
        } else {
            this.channelMarkerBtn.classList.remove('active');
            this.sendHoldRelease();
        }
    }
    
    toggleSelection(container) {
        container.classList.toggle('selected');
        container.classList.toggle('unselected');
        // TODO: Add to selection manager
    }
    
    sendPTTDown() {
        if (this.websocketManager.getWebSocketHandler(this.systemName)) {
            const message = {
                type: 'PTT_DOWN',
                data: {
                    channel: this.channelData.name,
                    system: this.systemName
                }
            };
            this.websocketManager.sendMessage(this.systemName, message);
        }
    }
    
    sendPTTUp() {
        if (this.websocketManager.getWebSocketHandler(this.systemName)) {
            const message = {
                type: 'PTT_UP',
                data: {
                    channel: this.channelData.name,
                    system: this.systemName
                }
            };
            this.websocketManager.sendMessage(this.systemName, message);
        }
    }
    
    sendPageDown() {
        // Page down logic
        this.audioManager.playTone('page', 1750);
    }
    
    sendPageUp() {
        // Page up logic
    }
    
    sendHold() {
        // Channel hold logic
    }
    
    sendHoldRelease() {
        // Channel hold release logic
    }
    
    setLastSrcId(srcId) {
        this.lastSrcId.textContent = `Last SRC: ${srcId}`;
    }
    
    setSystemName(name) {
        this.systemName = name;
        document.querySelector('.system-name', this.element).textContent = `System: ${name}`;
    }
    
    setEmergency(isEmergency) {
        this.isEmergency = isEmergency;
        if (isEmergency) {
            this.element.classList.add('emergency');
        } else {
            this.element.classList.remove('emergency');
        }
    }
    
    setIsReceiving(isReceiving) {
        this.isReceiving = isReceiving;
    }
}

/* ================================================
   Widget Factory - System Status Widget
   ================================================ */
class SystemStatusWidget {
    constructor(systemData) {
        this.systemData = systemData;
        this.element = this.create();
        this.isConnected = false;
    }
    
    create() {
        const container = document.createElement('div');
        container.className = 'system-status-widget disconnected';
        container.dataset.systemName = this.systemData.name;
        
        container.innerHTML = `
            <div class="system-status-content">
                <div class="system-name-display">${this.systemData.name}</div>
                <div class="connection-state">Disconnected</div>
            </div>
        `;
        
        // Store references
        this.connectionState = container.querySelector('.connection-state');
        
        // Click to toggle selection
        container.addEventListener('click', () => {
            // TODO: Handle system selection
        });
        
        return container;
    }
    
    setConnected(connected) {
        this.isConnected = connected;
        if (connected) {
            this.element.classList.remove('disconnected');
            this.element.classList.add('connected');
            this.connectionState.textContent = 'Connected';
        } else {
            this.element.classList.remove('connected');
            this.element.classList.add('disconnected');
            this.connectionState.textContent = 'Disconnected';
        }
    }
}

/* ================================================
   Widget Factory - Alert Tone Widget
   ================================================ */
class AlertToneWidget {
    constructor(alertPath) {
        this.alertPath = alertPath;
        this.element = this.create();
        this.alertName = this.extractAlertName(alertPath);
    }
    
    create() {
        const container = document.createElement('div');
        container.className = 'alert-tone-widget';
        container.dataset.alertPath = this.alertPath;
        
        container.innerHTML = `
            <div class="alert-tone-content">
                <button class="alert-tone-play-btn">
                    <img src="assets/page.png" alt="Play">
                </button>
                <input type="text" class="alert-tone-name" value="${this.alertName}" readonly>
            </div>
        `;
        
        // Store references
        this.playBtn = container.querySelector('.alert-tone-play-btn');
        
        // Play button click
        this.playBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.playAlert();
        });
        
        return container;
    }
    
    extractAlertName(path) {
        const parts = path.split('/');
        const filename = parts[parts.length - 1];
        return filename.replace('.wav', '');
    }
    
    playAlert() {
        // Play alert tone
        Utils.playAlertTone(this.alertPath);
    }
}

/* ================================================
   Dialog Manager
   ================================================ */
class DialogManager {
    constructor() {
        this.currentlyOpen = null;
    }
    
    openDialog(dialogId) {
        const dialog = DOM.dialogs[dialogId];
        if (!dialog) return;
        
        // Close any open dialog
        this.closeAll();
        
        dialog.classList.remove('hidden');
        this.currentlyOpen = dialogId;
        
        // Initialize dialog-specific content
        this.initializeDialog(dialogId);
    }
    
    closeDialog(dialogId) {
        const dialog = DOM.dialogs[dialogId];
        if (dialog) {
            dialog.classList.add('hidden');
            if (this.currentlyOpen === dialogId) {
                this.currentlyOpen = null;
            }
        }
    }
    
    closeAll() {
        Object.values(DOM.dialogs).forEach(dialog => {
            dialog.classList.add('hidden');
        });
        this.currentlyOpen = null;
    }
    
    initializeDialog(dialogId) {
        switch (dialogId) {
            case 'audioSettings':
                this.initAudioSettingsDialog();
                break;
            case 'digitalPage':
                this.initDigitalPageDialog();
                break;
            case 'keyStatus':
                this.initKeyStatusDialog();
                break;
            case 'callHistory':
                this.initCallHistoryDialog();
                break;
            case 'widgetSelection':
                this.initWidgetSelectionDialog();
                break;
            case 'keybindingConfig':
                this.initKeybindingConfigDialog();
                break;
        }
    }
    
    initAudioSettingsDialog() {
        // Populate input device dropdown
        if (audioManager.inputDevices.length > 0) {
            const select = DOM.inputDeviceSelect;
            select.innerHTML = '<option value="">Default</option>';
            
            audioManager.inputDevices.forEach((device, index) => {
                const option = document.createElement('option');
                option.value = device.deviceId;
                option.textContent = device.label || `Microphone ${index + 1}`;
                select.appendChild(option);
            });
        }
        
        // Populate PTT settings from AppState
        DOM.enablePttDownSound.checked = AppState.settings.enablePttDownSound;
        DOM.enablePttUpSound.checked = AppState.settings.enablePttUpSound;
        DOM.pttTailDelay.value = AppState.settings.pttTailDelayMs;
        
        // Populate channel output devices
        this.renderChannelOutputDevices();
    }
    
    initDigitalPageDialog() {
        // Populate system dropdown
        if (AppState.systems.length > 0) {
            const select = DOM.pageSystemSelect;
            select.innerHTML = '';
            
            AppState.systems.forEach(system => {
                const option = document.createElement('option');
                option.value = system.name;
                option.textContent = system.name;
                select.appendChild(option);
            });
        }
        
        DOM.pageDstId.value = '';
    }
    
    initKeyStatusDialog() {
        // Populate key status table
        const tbody = DOM.keyStatusTable.querySelector('tbody');
        tbody.innerHTML = '';
        
        // TODO: Populate with actual key status data
        const testRows = [
            { channel: 'Channel 1', algId: '01', keyId: '1', status: 'OK' },
            { channel: 'Channel 2', algId: '01', keyId: '2', status: 'OK' },
            { channel: 'Channel 3', algId: '01', keyId: '3', status: 'OK' }
        ];
        
        testRows.forEach(row => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>${row.channel}</td>
                <td>${row.algId}</td>
                <td>${row.keyId}</td>
                <td>${row.status}</td>
            `;
            tbody.appendChild(tr);
        });
    }
    
    initCallHistoryDialog() {
        // Populate call history table
        const tbody = DOM.callHistoryTable.querySelector('tbody');
        tbody.innerHTML = '';
        
        // TODO: Populate with actual call history data
        const testRows = [
            { channel: 'Channel 1', srcId: '123456789', dstId: '987654321' },
            { channel: 'Channel 2', srcId: '111111111', dstId: '222222222' }
        ];
        
        testRows.forEach(row => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>${row.channel}</td>
                <td>${row.srcId}</td>
                <td>${row.dstId}</td>
            `;
            tbody.appendChild(tr);
        });
    }
    
    initWidgetSelectionDialog() {
        DOM.showSystemStatus.checked = AppState.settings.showSystemStatus;
        DOM.showChannels.checked = AppState.settings.showChannels;
        DOM.showAlertTones.checked = AppState.settings.showAlertTones;
    }
    
    initKeybindingConfigDialog() {
        DOM.globalPttDisplay.value = AppState.settings.globalPttKeybind;
        DOM.hotkeysUnfocused.checked = AppState.settings.hotkeysWorkWhenUnfocused;
    }
    
    renderChannelOutputDevices() {
        const container = DOM.channelOutputDevices;
        container.innerHTML = '';
        
        // TODO: Populate with actual channels
        const testChannels = [
            { name: 'Channel 1', deviceId: '0' },
            { name: 'Channel 2', deviceId: '0' },
            { name: 'Channel 3', deviceId: '0' }
        ];
        
        testChannels.forEach((channel, index) => {
            const row = document.createElement('div');
            row.className = 'channel-device-row';
            
            const nameSpan = document.createElement('span');
            nameSpan.className = 'channel-device-name';
            nameSpan.textContent = channel.name;
            
            const select = document.createElement('select');
            select.className = 'channel-device-select';
            
            // Populate output devices
            audioManager.outputDevices.forEach((device, devIndex) => {
                const option = document.createElement('option');
                option.value = device.deviceId;
                option.textContent = device.label || `Output ${devIndex + 1}`;
                if (device.deviceId === channel.deviceId) {
                    option.selected = true;
                }
                select.appendChild(option);
            });
            
            row.appendChild(nameSpan);
            row.appendChild(select);
            container.appendChild(row);
        });
    }
}

/* ================================================
    Menu Manager
    ================================================ */
const MenuManager = {
    init: () => {
        // Close submenus when clicking elsewhere
        // Use a single global handler to avoid duplicates
        if (!MenuManager._globalClickHandler) {
            MenuManager._globalClickHandler = (e) => {
                // Check if click was on a menu label
                if (e.target.classList.contains('menu-label')) {
                    return; // Let the menu label handler deal with it
                }
                // Check if click was on a submenu
                if (e.target.closest('.submenu')) {
                    return; // Let submenu items deal with it
                }
                MenuManager.closeAllSubmenus();
            };
            document.addEventListener('click', MenuManager._globalClickHandler);
        }
        
        // Open submenu when clicking menu label
        DOM.menuLabels.forEach(label => {
            label.addEventListener('click', (e) => {
                e.stopPropagation();
                const submenu = label.nextElementSibling;
                if (submenu && submenu.classList.contains('submenu')) {
                    MenuManager.toggleSubmenu(submenu);
                }
            });
        });
        
        // Handle submenu item clicks
        document.querySelectorAll('.submenu-item').forEach(item => {
            item.addEventListener('click', (e) => {
                e.stopPropagation();
                const action = item.dataset.action;
                MenuManager.handleAction(action);
                MenuManager.closeAllSubmenus();
            });
        });
    },
    
    toggleSubmenu: (submenu) => {
        MenuManager.closeAllSubmenus();
        if (submenu) {
            submenu.classList.toggle('show');
        }
    },
    
    closeAllSubmenus: () => {
        document.querySelectorAll('.submenu').forEach(submenu => {
            submenu.classList.remove('show');
        });
    },
    
    handleAction: (action) => {
        switch (action) {
            case 'open-codeplug':
                DOM.fileInputCodeplug.click();
                break;
            case 'audio-settings':
                dialogManager.openDialog('audioSettings');
                break;
            case 'keybindings':
                dialogManager.openDialog('keybindingConfig');
                break;
            case 'toggle-edit-mode':
                AppState.isEditMode = !AppState.isEditMode;
                alert(`Edit mode: ${AppState.isEditMode ? 'Enabled' : 'Disabled'}`);
                break;
            case 'select-widgets':
                dialogManager.openDialog('widgetSelection');
                break;
            case 'reset-settings':
                localStorage.removeItem('whackerlink_settings');
                alert('Settings reset. Please reload the page.');
                break;
            case 'add-alert-tone':
                DOM.fileInputAlertTone.click();
                break;
            case 'p25-page':
                dialogManager.openDialog('digitalPage');
                break;
            case 'manual-page':
                dialogManager.openDialog('manualPage');
                break;
        }
    }
};

/* ================================================
   Toolbar Manager
   ================================================ */
const ToolbarManager = {
    init: () => {
        DOM.btnAlert1.addEventListener('click', () => App.sendAlertTone(1));
        DOM.btnAlert2.addEventListener('click', () => App.sendAlertTone(2));
        DOM.btnAlert3.addEventListener('click', () => App.sendAlertTone(3));
        DOM.btnGlobalPtt.addEventListener('click', () => App.toggleGlobalPtt());
        DOM.btnClearEmergency.addEventListener('click', () => App.clearEmergency());
        DOM.btnOpenCodeplug.addEventListener('click', () => DOM.fileInputCodeplug.click());
        DOM.btnP25Page.addEventListener('click', () => dialogManager.openDialog('digitalPage'));
        DOM.btnSelectAll.addEventListener('click', () => App.selectAllChannels());
        DOM.btnKeyStats.addEventListener('click', () => dialogManager.openDialog('keyStatus'));
        DOM.btnCallHistory.addEventListener('click', () => dialogManager.openDialog('callHistory'));
        DOM.btnAudioSettings.addEventListener('click', () => dialogManager.openDialog('audioSettings'));
    },
    
    updateGlobalPtt: (state) => {
        if (state) {
            DOM.btnGlobalPtt.style.background = 'linear-gradient(to bottom, #FF0000 48.5%, #D50000 51.7%)';
        } else {
            DOM.btnGlobalPtt.style.background = 'linear-gradient(to bottom, #F0F0F0 48.5%, #DBDBDB 51.7%)';
        }
    }
};

/* ================================================
   Main Application
   ================================================ */
const App = {
    audioManager: new AudioManager(),
    websocketManager: new WebSocketManager(),
    dialogManager: new DialogManager(),
    
    init: async () => {
        console.log('WhackerLinkConsoleV2 - Web Edition');
        console.log('Initializing...');
        
        // Load settings
        Utils.loadSettings();
        
        // Initialize audio
        await App.audioManager.init();
        
        // Initialize menu
        MenuManager.init();
        
        // Initialize toolbar
        ToolbarManager.init();
        
        // Initialize dialog close buttons
        App.initDialogCloseButtons();
        
        // Initialize file inputs
        App.initFileInputs();
        
        // Initialize tabs
        App.initTabs();
        
        // Apply saved settings
        App.applySettings();
        
        console.log('Initialization complete!');
    },
    
    initDialogCloseButtons: () => {
        document.querySelectorAll('.dialog-close').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                const dialogId = btn.dataset.close;
                dialogManager.closeDialog(dialogId);
            });
        });
    },
    
    initFileInputs: () => {
        // Codeplug file input
        DOM.fileInputCodeplug.addEventListener('change', async (e) => {
            const file = e.target.files[0];
            if (file) {
                await App.loadCodeplug(file);
                MenuManager.closeAllSubmenus();
            }
        });
        
        // Alert tone file input
        DOM.fileInputAlertTone.addEventListener('change', async (e) => {
            const file = e.target.files[0];
            if (file) {
                App.addAlertTone(file.name);
                MenuManager.closeAllSubmenus();
            }
        });
    },
    
    addAlertTone: (filename) => {
        // Add alert tone to settings
        const alertPaths = AppState.settings.alertToneFilePaths;
        if (!alertPaths.includes(filename)) {
            alertPaths.push(filename);
            Utils.saveSettings();
            console.log('Alert tone added:', filename);
        }
    },
    
    initTabs: () => {
        DOM.tabBtns.forEach(btn => {
            btn.addEventListener('click', () => {
                // Remove active class from all tabs
                DOM.tabBtns.forEach(b => b.classList.remove('active'));
                
                // Hide all content
                DOM.pttKeybindingsContent.classList.add('hidden');
                DOM.channelToggleContent.classList.add('hidden');
                
                // Show selected content
                btn.classList.add('active');
                const tab = btn.dataset.tab;
                if (tab === 'ptt-keybindings') {
                    DOM.pttKeybindingsContent.classList.remove('hidden');
                } else {
                    DOM.channelToggleContent.classList.remove('hidden');
                }
            });
        });
    },
    
    applySettings: () => {
        // Apply saved settings to UI
        if (AppState.settings.showSystemStatus === false) {
            // TODO: Hide system status widgets
        }
        if (AppState.settings.showChannels === false) {
            // TODO: Hide channel widgets
        }
        if (AppState.settings.showAlertTones === false) {
            // TODO: Hide alert tone widgets
        }
    },
    
    loadCodeplug: async (file) => {
        try {
            const codeplug = await Utils.loadCodeplug(file);
            AppState.codeplug = codeplug;
            console.log('Codeplug loaded:', codeplug);
            console.log('Systems:', codeplug.systems);
            console.log('Zones:', codeplug.zones);
            
            // Generate widgets
            App.generateWidgets();
            
            alert(`Codeplug loaded: ${file.name}\nSystems: ${codeplug.systems?.length || 0}\nChannels: ${codeplug.zones?.reduce((acc, z) => acc + (z.channels?.length || 0), 0) || 0}`);
        } catch (err) {
            console.error('Failed to load codeplug:', err);
            alert('Failed to load codeplug: ' + err.message);
        }
    },
    
    generateWidgets: () => {
        const canvas = DOM.channelsCanvas;
        canvas.innerHTML = '';
        
        if (!AppState.codeplug) return;
        
        // Generate system status widgets
        if (AppState.settings.showSystemStatus) {
            AppState.codeplug.systems.forEach(system => {
                const widget = new SystemStatusWidget(system);
                canvas.appendChild(widget.element);
                AppState.widgets.push(widget);
            });
        }
        
        // Generate channel widgets
        if (AppState.settings.showChannels) {
            AppState.codeplug.zones.forEach(zone => {
                zone.channels.forEach(channel => {
                    const widget = new ChannelWidget(
                        { name: channel.name, system: channel.system || 'Default' },
                        App.audioManager,
                        App.websocketManager
                    );
                    canvas.appendChild(widget.element);
                    AppState.widgets.push(widget);
                });
            });
        }
        
        // Generate alert tone widgets
        if (AppState.settings.showAlertTones) {
            AppState.settings.alertToneFilePaths.forEach(path => {
                const widget = new AlertToneWidget(path);
                canvas.appendChild(widget.element);
                AppState.widgets.push(widget);
            });
        }
    },
    
    sendAlertTone: (toneNumber) => {
        const audioManager = App.audioManager;
        const alertPaths = AppState.settings.alertToneFilePaths;
        
        if (alertPaths.length > 0) {
            const path = alertPaths[toneNumber - 1] || alertPaths[0];
            audioManager.playAlertTone(path);
        } else {
            // Play default alert tone
            audioManager.playTone('alert', 1000 + (toneNumber * 200));
        }
    },
    
    toggleGlobalPtt: () => {
        AppState.globalPttState = !AppState.globalPttState;
        ToolbarManager.updateGlobalPtt(AppState.globalPttState);
        
        // TODO: Send PTT command to all selected channels
        console.log('Global PTT:', AppState.globalPttState);
        
        if (AppState.globalPttState) {
            App.audioManager.playTone('pttDown', 440);
        } else {
            App.audioManager.playTone('pttUp', 880);
        }
    },
    
    clearEmergency: () => {
        // Clear emergency alert
        document.querySelectorAll('.channel-widget.emergency').forEach(widget => {
            widget.classList.remove('emergency');
        });
        console.log('Emergency cleared');
    },
    
    selectAllChannels: () => {
        // Select all channels
        document.querySelectorAll('.channel-widget').forEach(widget => {
            widget.classList.add('selected');
            widget.classList.remove('unselected');
        });
        console.log('All channels selected');
    }
};

/* ================================================
   Global Dialog Manager Instance
   ================================================ */
const dialogManager = App.dialogManager;
const audioManager = App.audioManager;

/* ================================================
   Global Event Handlers
   ================================================ */
document.addEventListener('DOMContentLoaded', async () => {
    // Initialize the application
    await App.init();
});

// Handle global PTT hotkey (default: Ctrl+T)
document.addEventListener('keydown', (e) => {
    if (e.ctrlKey && e.key === 't') {
        e.preventDefault();
        App.toggleGlobalPtt();
    }
});

// Handle escape to close dialogs
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && dialogManager.currentlyOpen) {
        dialogManager.closeDialog(dialogManager.currentlyOpen);
    }
});

// Handle dialog footer buttons
document.getElementById('audio-settings-save').addEventListener('click', () => {
    // Save settings
    AppState.settings.enablePttDownSound = DOM.enablePttDownSound.checked;
    AppState.settings.enablePttUpSound = DOM.enablePttUpSound.checked;
    AppState.settings.pttTailDelayMs = parseInt(DOM.pttTailDelay.value) || 0;
    
    // Save to localStorage
    Utils.saveSettings();
    
    dialogManager.closeDialog('audioSettings');
});

document.getElementById('audio-settings-cancel').addEventListener('click', () => {
    dialogManager.closeDialog('audioSettings');
});

document.getElementById('page-send-btn').addEventListener('click', () => {
    const system = DOM.pageSystemSelect.value;
    const dstId = DOM.pageDstId.value;
    
    if (system && dstId) {
        // Send page command
        console.log(`Sending P25 Page to ${dstId} on system ${system}`);
        
        // TODO: Send actual page command via WebSocket
        alert(`Page sent to ${dstId}`);
        dialogManager.closeDialog('digitalPage');
    } else {
        alert('Please select a system and enter a destination ID');
    }
});

document.getElementById('manual-page-send-btn').addEventListener('click', () => {
    const toneA = DOM.manualToneA.value;
    const toneB = DOM.manualToneB.value;
    
    if (toneA && toneB) {
        console.log(`Sending Manual QC2: A=${toneA}, B=${toneB}`);
        
        // TODO: Send manual page command
        alert('Manual page sent');
        dialogManager.closeDialog('manualPage');
    } else {
        alert('Please enter both A and B tones');
    }
});

document.getElementById('widget-selection-apply').addEventListener('click', () => {
    AppState.settings.showSystemStatus = DOM.showSystemStatus.checked;
    AppState.settings.showChannels = DOM.showChannels.checked;
    AppState.settings.showAlertTones = DOM.showAlertTones.checked;
    
    Utils.saveSettings();
    dialogManager.closeDialog('widgetSelection');
    
    // Regenerate widgets
    App.generateWidgets();
});

document.getElementById('keybinding-apply').addEventListener('click', () => {
    AppState.settings.globalPttKeybind = DOM.globalPttDisplay.value;
    AppState.settings.hotkeysWorkWhenUnfocused = DOM.hotkeysUnfocused.checked;
    
    Utils.saveSettings();
    dialogManager.closeDialog('keybindingConfig');
});

document.getElementById('keybinding-close').addEventListener('click', () => {
    dialogManager.closeDialog('keybindingConfig');
});

document.getElementById('record-global-ptt-btn').addEventListener('click', () => {
    // TODO: Implement key recording
    alert('Key recording not yet implemented');
});

document.getElementById('clear-global-ptt-btn').addEventListener('click', () => {
    DOM.globalPttDisplay.value = '';
    AppState.settings.globalPttKeybind = '';
    Utils.saveSettings();
});

// Alert tone play button
document.querySelectorAll('.alert-tone-play-btn').forEach(btn => {
    btn.addEventListener('click', (e) => {
        e.stopPropagation();
        // Play alert tone
        const widget = btn.closest('.alert-tone-widget');
        const alertPath = widget ? widget.dataset.alertPath : null;
        if (alertPath) {
            audioManager.playAlertTone(alertPath);
        }
    });
});