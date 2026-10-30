mergeInto(LibraryManager.library, {
  StarlightCopyText: function(pointer) {
    var text = UTF8ToString(pointer);
    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).catch(function() { window.prompt('참가 코드를 복사하세요', text); });
    } else { window.prompt('참가 코드를 복사하세요', text); }
  }
});
