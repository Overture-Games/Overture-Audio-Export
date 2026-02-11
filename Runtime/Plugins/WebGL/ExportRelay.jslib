var LibraryExportRelay = {
  $BridgeState: {
    initialized: false,
    gameObjectName: null,
    messageHandler: null
  },

  // Helper: safely get BridgeState even if $BridgeState deps failed
  $BridgeState__postset: 'if (typeof BridgeState === "undefined") { BridgeState = { initialized: false, gameObjectName: null, messageHandler: null }; }',

  // Helper: safe SendMessage that never throws
  _OB_SendMessage: function(gameObjectName, methodName, param) {
    try {
      if (typeof Module !== 'undefined' && Module && typeof Module.SendMessage === 'function') {
        Module.SendMessage(gameObjectName, methodName, param);
      } else {
        console.warn('[Unity Bridge] Module.SendMessage not available, cannot call ' + methodName);
      }
    } catch (e) {
      console.error('[Unity Bridge] SendMessage failed for ' + methodName + ':', e);
    }
  },

  // Initialize the Bridge message listener (call once)
  OvertureBridge_Init__deps: ['$BridgeState', '_OB_SendMessage'],
  OvertureBridge_Init: function(gameObjectNamePtr) {
    try {
      if (BridgeState.initialized) return;

      BridgeState.gameObjectName = UTF8ToString(gameObjectNamePtr);
      BridgeState.initialized = true;

      BridgeState.messageHandler = function(event) {
        try {
          var data = event.data;
          if (!data || typeof data.type !== 'string' || !data.type.startsWith('OVERTURE_')) {
            return;
          }

          console.log('[Unity Bridge] Received:', data.type, data);

          var payload = data.payload || {};
          var goName = BridgeState.gameObjectName;

          switch (data.type) {
            case 'OVERTURE_HANDSHAKE_RESPONSE':
              __OB_SendMessage(goName, 'OnBridgeHandshakeResult', JSON.stringify({
                supported: payload.supported || false,
                capabilities: payload.capabilities || [],
                version: payload.version || null,
                requestId: data.requestId || ''
              }));
              break;

            case 'OVERTURE_SAVE_SONG_ACK':
              __OB_SendMessage(goName, 'OnBridgeSaveAck', data.requestId || '');
              break;

            case 'OVERTURE_SAVE_SONG_PROGRESS':
              __OB_SendMessage(goName, 'OnBridgeSaveProgress', JSON.stringify({
                requestId: data.requestId || '',
                percent: payload.percent || 0,
                stage: payload.stage || ''
              }));
              break;

            case 'OVERTURE_SAVE_SONG_RESPONSE':
              __OB_SendMessage(goName, 'OnBridgeSaveResult', JSON.stringify({
                requestId: data.requestId || '',
                success: payload.success || false,
                songId: payload.songId || null,
                error: payload.error || null
              }));
              break;
          }
        } catch (handlerError) {
          console.error('[Unity Bridge] Error in message handler:', handlerError);
        }
      };

      window.addEventListener('message', BridgeState.messageHandler);
      console.log('[Unity Bridge] Message listener initialized for:', BridgeState.gameObjectName);
    } catch (e) {
      console.error('[Unity Bridge] Init failed:', e);
    }
  },

  // Send handshake request to parent window
  OvertureBridge_Handshake: function(requestIdPtr) {
    try {
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
    } catch (e) {
      console.error('[Unity Bridge] Handshake failed:', e);
    }
  },

  // Save song via Bridge protocol
  OvertureBridge_SaveSong__deps: ['$BridgeState', '_OB_SendMessage'],
  OvertureBridge_SaveSong: function(requestIdPtr, songDataJsonPtr) {
    try {
      var requestId = UTF8ToString(requestIdPtr);
      var songDataJson = UTF8ToString(songDataJsonPtr);
      var songData;

      try {
        songData = JSON.parse(songDataJson);
      } catch (parseErr) {
        console.error('[Unity Bridge] Failed to parse song data:', parseErr);
        // Send error back to C# so it can fall back to legacy
        var goName = BridgeState.initialized ? BridgeState.gameObjectName : null;
        if (goName) {
          __OB_SendMessage(goName, 'OnBridgeSaveResult', JSON.stringify({
            requestId: requestId || '',
            success: false,
            songId: null,
            error: 'JSON parse error: ' + (parseErr.message || 'unknown')
          }));
        }
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
        var goName = BridgeState.initialized ? BridgeState.gameObjectName : null;
        if (goName) {
          __OB_SendMessage(goName, 'OnBridgeSaveResult', JSON.stringify({
            requestId: requestId || '',
            success: false,
            songId: null,
            error: 'No parent window available'
          }));
        }
      }
    } catch (e) {
      console.error('[Unity Bridge] SaveSong failed:', e);
    }
  },

  // Legacy: Direct API call to OverturePlatform.saveSong
  SaveSong: function(songDataJsonPtr, gameObjectNamePtr) {
    var songDataJson, gameObjectName;
    try {
      songDataJson = UTF8ToString(songDataJsonPtr);
      gameObjectName = UTF8ToString(gameObjectNamePtr);
    } catch (e) {
      console.error('[Unity Legacy] Failed to read string parameters:', e);
      return;
    }

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

        if (typeof Module !== 'undefined' && Module && typeof Module.SendMessage === 'function') {
          Module.SendMessage(gameObjectName, 'OnPlatformUploadResult', successPayload);
        }
      } catch (error) {
        console.error('[Unity Legacy] saveSong failed:', error);

        var errorPayload = JSON.stringify({
          success: false,
          message: (error && error.message) ? error.message : 'Unknown error',
          songId: null
        });

        try {
          if (typeof Module !== 'undefined' && Module && typeof Module.SendMessage === 'function') {
            Module.SendMessage(gameObjectName, 'OnPlatformUploadResult', errorPayload);
          }
        } catch (sendErr) {
          console.error('[Unity Legacy] Failed to send error result back to Unity:', sendErr);
        }
      }
    })();
  }
};

mergeInto(LibraryManager.library, LibraryExportRelay);
