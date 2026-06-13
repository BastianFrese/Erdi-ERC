document.addEventListener('click', function (ev) {
    var cancel = ev.target.closest('[data-consent-cancel]');
    if (cancel) {
        var overlay = ev.target.closest('[data-setup-consent]');
        if (overlay) overlay.remove();
        return;
    }
});

// Intercept form submission to ensure checkbox is checked
document.addEventListener('submit', function (ev) {
    var form = ev.target.closest('[data-accept-setup-form]');
    if (!form) return;
    var checkbox = form.querySelector('input[name="accept"]');
    if (!checkbox || !checkbox.checked) {
        ev.preventDefault();
        alert('Bitte bestätige die Widerrufsbelehrung, indem du das Kästchen anklickst.');
    }
});