var LibraryExportRelay = {
  $BridgeState: {
    initialized: false,
    gameObjectName: null,
    messageHandler: null
  },

  // Initialize the Bridge message listener (call once)
  OvertureBridge_Init: function(gameObjectNamePtr) {
    if (BridgeState.initialized) return;

    BridgeState.gameObjectName = UTF8ToString(gameObjectNamePtr);
    BridgeState.initialized = true;

    BridgeState.messageHandler = function(event) {
      var data = event.data;
      if (!data || typeof data.type !== 'string' || !data.type.startsWith('OVERTURE_')) {
        return;
      }

      console.log('[Unity Bridge] Received:', data.type, data);

      switch (data.type) {
        case 'OVERTURE_HANDSHAKE_RESPONSE':
          if (Module && typeof Module.SendMessage === 'function') {
            Module.SendMessage(BridgeState.gameObjectName, 'OnBridgeHandshakeResult', JSON.stringify({
              supported: data.payload.supported,
              capabilities: data.payload.capabilities,
              version: data.payload.version,
              requestId: data.requestId
            }));
          }
          break;

        case 'OVERTURE_SAVE_SONG_ACK':
          if (Module && typeof Module.SendMessage === 'function') {
            Module.SendMessage(BridgeState.gameObjectName, 'OnBridgeSaveAck', data.requestId || '');
          }
          break;

        case 'OVERTURE_SAVE_SONG_PROGRESS':
          if (Module && typeof Module.SendMessage === 'function') {
            Module.SendMessage(BridgeState.gameObjectName, 'OnBridgeSaveProgress', JSON.stringify({
              requestId: data.requestId,
              percent: data.payload.percent,
              stage: data.payload.stage
            }));
          }
          break;

        case 'OVERTURE_SAVE_SONG_RESPONSE':
          if (Module && typeof Module.SendMessage === 'function') {
            Module.SendMessage(BridgeState.gameObjectName, 'OnBridgeSaveResult', JSON.stringify({
              requestId: data.requestId,
              success: data.payload.success,
              songId: data.payload.songId || null,
              error: data.payload.error || null
            }));
          }
          break;
      }
    };

    window.addEventListener('message', BridgeState.messageHandler);
    console.log('[Unity Bridge] Message listener initialized for:', BridgeState.gameObjectName);
  },

  // Send handshake request to parent window
  OvertureBridge_Handshake: function(requestIdPtr) {
    var requestId = UTF8ToString(requestIdPtr);
    var msg = {
      type: 'OVERTURE_HANDSHAKE_REQUEST',
      requestId: requestId,
      payload: {
        gameId: 'unity-game'
      }
    };

    console.log('[Unity Bridge] Sending handshake:', msg);

    if (window.parent && window.parent !== window) {
      window.parent.postMessage(msg, '*');
    } else {
      console.warn('[Unity Bridge] No parent window available for handshake');
    }
  },

  // Save song via Bridge protocol
  OvertureBridge_SaveSong: function(requestIdPtr, songDataJsonPtr) {
    var requestId = UTF8ToString(requestIdPtr);
    var songDataJson = UTF8ToString(songDataJsonPtr);
    var songData;

    try {
      songData = JSON.parse(songDataJson);
    } catch (e) {
      console.error('[Unity Bridge] Failed to parse song data:', e);
      return;
    }

    var msg = {
      type: 'OVERTURE_SAVE_SONG',
      requestId: requestId,
      payload: songData
    };

    console.log('[Unity Bridge] Sending save request:', requestId);

    if (window.parent && window.parent !== window) {
      window.parent.postMessage(msg, '*');
    } else {
      console.warn('[Unity Bridge] No parent window available for save');
    }
  },

  // Legacy: Direct API call to OverturePlatform.saveSong
  SaveSong: function(songDataJsonPtr, gameObjectNamePtr) {
    var songDataJson = UTF8ToString(songDataJsonPtr);
    var gameObjectName = UTF8ToString(gameObjectNamePtr);

    (async function() {
      try {
        console.log('[Unity Legacy] Calling Platform saveSong API...');
        var songData = JSON.parse(songDataJson);

        // Choose host: iframe's window or parent window
        var host = null;
        if (window.OverturePlatform && typeof window.OverturePlatform.saveSong === 'function') {
          host = window;
        } else if (window.parent && window.parent.OverturePlatform && typeof window.parent.OverturePlatform.saveSong === 'function') {
          host = window.parent;
        }

        if (!host) {
          throw new Error('Platform saveSong API not available on iframe or parent');
        }

        var songId = await host.OverturePlatform.saveSong(songData);
        console.log('[Unity Legacy] Song saved with ID:', songId);

        var successPayload = JSON.stringify({
          success: true,
          message: 'DAW composition saved successfully',
          songId: songId
        });

        if (Module && typeof Module.SendMessage === 'function') {
          Module.SendMessage(gameObjectName, 'OnPlatformUploadResult', successPayload);
        }
      } catch (error) {
        console.error('[Unity Legacy] saveSong failed:', error);

        var errorPayload = JSON.stringify({
          success: false,
          message: error.message || 'Unknown error',
          songId: null
        });

        if (Module && typeof Module.SendMessage === 'function') {
          Module.SendMessage(gameObjectName, 'OnPlatformUploadResult', errorPayload);
        }
      }
    })();
  }
};

mergeInto(LibraryManager.library, LibraryExportRelay);
