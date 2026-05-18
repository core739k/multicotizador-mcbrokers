// Loading overlay opt-in para operaciones admin largas.
//
// Uso: agregar data-loading-message="Texto a mostrar..." al <form>. Al hacer
// submit, deshabilita todos los botones del form, muestra un overlay
// fullscreen con spinner + mensaje y bloquea cualquier interacción hasta que
// el server responda (la página se recarga al recibir respuesta y el overlay
// desaparece naturalmente).
//
// El overlay se inyecta dinámicamente la primera vez que se necesita; no hace
// falta marcado adicional en cada página. beforeunload se usa solo dentro del
// submit en curso, no globalmente.

(function () {
    'use strict';

    const OVERLAY_ID = 'mcb-loading-overlay';

    function ensureOverlay() {
        let overlay = document.getElementById(OVERLAY_ID);
        if (overlay) return overlay;

        overlay = document.createElement('div');
        overlay.id = OVERLAY_ID;
        overlay.setAttribute('role', 'status');
        overlay.setAttribute('aria-live', 'polite');
        overlay.style.cssText = [
            'position:fixed', 'inset:0',
            'background:rgba(0,0,0,0.55)',
            'display:none',
            'align-items:center', 'justify-content:center',
            'z-index:2000',
            'cursor:wait'
        ].join(';');

        const card = document.createElement('div');
        card.style.cssText = [
            'background:#fff',
            'padding:2rem 2.5rem',
            'border-radius:0.5rem',
            'box-shadow:0 0.5rem 1.5rem rgba(0,0,0,0.3)',
            'display:flex', 'flex-direction:column',
            'align-items:center', 'gap:1rem',
            'min-width:18rem', 'max-width:90vw',
            'text-align:center'
        ].join(';');

        const spinner = document.createElement('div');
        spinner.className = 'spinner-border text-primary';
        spinner.setAttribute('role', 'status');
        spinner.style.width = '3rem';
        spinner.style.height = '3rem';
        spinner.innerHTML = '<span class="visually-hidden">Cargando…</span>';

        const message = document.createElement('div');
        message.id = OVERLAY_ID + '-message';
        message.style.cssText = 'font-size:1.05rem;color:#212529;';

        card.appendChild(spinner);
        card.appendChild(message);
        overlay.appendChild(card);
        document.body.appendChild(overlay);

        return overlay;
    }

    function show(message) {
        const overlay = ensureOverlay();
        const msgEl = document.getElementById(OVERLAY_ID + '-message');
        msgEl.textContent = message || 'Procesando, por favor espere…';
        overlay.style.display = 'flex';
        document.body.style.overflow = 'hidden';
    }

    function disableForm(form) {
        const controls = form.querySelectorAll('button, input[type=submit], input[type=button]');
        controls.forEach(c => {
            c.disabled = true;
            c.setAttribute('aria-disabled', 'true');
        });
    }

    function preventDoubleSubmit(form) {
        // Marcar que ya está procesando; cualquier submit subsecuente del mismo
        // form se cancela (defensa contra doble click rápido o Enter repetido).
        form.dataset.loadingActive = '1';
    }

    function attachToForm(form) {
        if (form.dataset.loadingBound === '1') return;
        form.dataset.loadingBound = '1';

        form.addEventListener('submit', function (ev) {
            if (form.dataset.loadingActive === '1') {
                ev.preventDefault();
                ev.stopImmediatePropagation();
                return;
            }
            const msg = form.getAttribute('data-loading-message');
            preventDoubleSubmit(form);
            disableForm(form);
            show(msg);
        });
    }

    function init() {
        const forms = document.querySelectorAll('form[data-loading-message]');
        forms.forEach(attachToForm);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
