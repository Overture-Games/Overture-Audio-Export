var LibraryExportRelay = {
  SaveSong: function(songDataJsonPtr, gameObjectNamePtr) {
    var songDataJson   = UTF8ToString(songDataJsonPtr);
    var gameObjectName = UTF8ToString(gameObjectNamePtr);

    (async function() {
      try {
        console.log('🎵 DAW Export: Unity calling Platform saveSong API...');
        var songData = JSON.parse(songDataJson);
        console.log('📊 DAW Export Song data:', songData);

        // Choose host: iframe’s window or parent window
        var host = null;
        if (window.OverturePlatform && typeof window.OverturePlatform.saveSong === 'function') {
          host = window;
        } else if (window.parent && window.parent.OverturePlatform && typeof window.parent.OverturePlatform.saveSong === 'function') {
          host = window.parent;
        }

        if (!host) {
          throw new Error('Platform saveSong API not available on iframe or parent');
        }

        // Call the host’s saveSong
        var songId = await host.OverturePlatform.saveSong(songData);
        console.log('✅ DAW Export: Song saved successfully with ID:', songId);

        var successPayload = JSON.stringify({
          success: true,
          message: 'DAW composition saved successfully',
          songId: songId
        });

        if (Module && typeof Module.SendMessage === 'function') {
          Module.SendMessage(gameObjectName, 'OnPlatformUploadResult', successPayload);
        } else {
          console.error('JSLib Error: Module.SendMessage is not a function!');
        }
      } catch (error) {
        console.error('❌ DAW Export: Platform saveSong failed:', error);

        var errorPayload = JSON.stringify({
          success: false,
          message: error.message || 'Unknown error',
          songId: null
        });

        if (Module && typeof Module.SendMessage === 'function') {
          Module.SendMessage(gameObjectName, 'OnPlatformUploadResult', errorPayload);
        } else {
          console.error('JSLib Error: Module.SendMessage is not available to send error!');
        }
      }
    })();
  }
};

mergeInto(LibraryManager.library, LibraryExportRelay);
