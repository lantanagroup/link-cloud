(function () {
    var id = new URLSearchParams(window.location.search).get("edit");
    if (!id || !window.CSS || !CSS.escape) return;
    var button = document.querySelector("[data-mode][data-id='" + CSS.escape(id) + "']");
    if (button) button.click();
})();
