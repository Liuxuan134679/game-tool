mergeInto(LibraryManager.library, {
  $AreaGridAudio__deps: ['$WEBAudio'],
  $AreaGridAudio: {
    muted: false,
    resume: function () {
      if (AreaGridAudio.muted || document.hidden) return;
      // Feature-detected iOS media routing; never request microphone access.
      try { if (navigator.audioSession) navigator.audioSession.type = 'playback'; } catch (_) {}
      var context = WEBAudio.audioContext;
      if (context && context.state !== 'running' && context.state !== 'closed') {
        try { var pending = context.resume(); if (pending) pending.catch(function () {}); } catch (_) {}
      }
    }
  },
  AreaGridSetAudioMuted__deps: ['$AreaGridAudio'],
  AreaGridSetAudioMuted: function (muted) {
    AreaGridAudio.muted = !!muted;
    if (!muted) AreaGridAudio.resume();
    else { try { if (navigator.audioSession) navigator.audioSession.type = 'auto'; } catch (_) {} }
  },
  AreaGridInstallLifecycle__deps: ['$AreaGridAudio'],
  AreaGridInstallLifecycle: function (namePtr) {
    var gameObjectName = UTF8ToString(namePtr);
    if (window.__areaGridLifecycle) return;
    window.__areaGridLifecycle = true;
    function pause() { try { SendMessage(gameObjectName, 'BrowserHidden'); } catch (_) {} }
    document.addEventListener('visibilitychange', function () { if (document.hidden) pause(); });
    window.addEventListener('pagehide', pause);
    window.addEventListener('blur', pause);
    // Resume inside the trusted gesture, before Unity handles its button click.
    var events = ['pointerdown', 'pointerup', 'touchend', 'keydown'];
    events.forEach(function (event) { window.addEventListener(event, AreaGridAudio.resume, true); });
    Module.deinitializers.push(function () {
      events.forEach(function (event) { window.removeEventListener(event, AreaGridAudio.resume, true); });
    });
  }
});
