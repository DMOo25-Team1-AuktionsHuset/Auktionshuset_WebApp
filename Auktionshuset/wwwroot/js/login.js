document.addEventListener("submit", event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || form.id !== "login-form") return;
    if (form.dataset.submitting === "true") {
        event.preventDefault();
        return;
    }

    form.dataset.submitting = "true";
    const button = form.querySelector('button[type="submit"]');
    if (button) {
        button.disabled = true;
        button.textContent = "Logger ind...";
        button.setAttribute("aria-busy", "true");
    }
});
