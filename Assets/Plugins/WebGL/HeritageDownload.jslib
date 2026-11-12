mergeInto(LibraryManager.library, {
  // Saves a text file (the study results CSV) through the browser's download mechanism.
  HeritageDownloadText: function (filename, content) {
    var name = UTF8ToString(filename), text = UTF8ToString(content);
    var blob = new Blob(["﻿" + text], { type: "text/csv;charset=utf-8" });
    var a = document.createElement("a");
    a.href = URL.createObjectURL(blob); a.download = name; a.style.display = "none";
    document.body.appendChild(a); a.click();
    setTimeout(function () { document.body.removeChild(a); URL.revokeObjectURL(a.href); }, 800);
  }
});
