(() => {
    'use strict';

    let deferredInstallPrompt = null;
    const installButtons = () => document.querySelectorAll('[data-pwa-install]');
    const networkBanner = document.getElementById('networkStatus');

    function setInstallButtonsVisible(visible) {
        installButtons().forEach(button => {
            button.classList.toggle('d-none', !visible);
        });
    }

    function normalizePath(value) {
        if (!value) return '/';
        const path = value.split('?')[0].split('#')[0];
        return path.length > 1 && path.endsWith('/') ? path.slice(0, -1) : path;
    }

    function updateActiveNavigation() {
        const currentPath = normalizePath(window.location.pathname).toLowerCase();
        const links = Array.from(document.querySelectorAll('[data-nav-path]'));

        let bestMatchLength = -1;
        let bestMatchPath = null;

        links.forEach(link => {
            const configuredPath = normalizePath(link.getAttribute('data-nav-path')).toLowerCase();
            const matches = currentPath === configuredPath || currentPath.startsWith(`${configuredPath}/`);

            if (matches && configuredPath.length > bestMatchLength) {
                bestMatchLength = configuredPath.length;
                bestMatchPath = configuredPath;
            }
        });

        links.forEach(link => {
            const configuredPath = normalizePath(link.getAttribute('data-nav-path')).toLowerCase();
            link.classList.toggle('is-active', configuredPath === bestMatchPath);
            if (configuredPath === bestMatchPath) {
                link.setAttribute('aria-current', 'page');
            } else {
                link.removeAttribute('aria-current');
            }
        });
    }

    function closeMobileDrawerOnNavigation() {
        const drawerElement = document.getElementById('mobileMenu');
        if (!drawerElement || typeof bootstrap === 'undefined') return;

        document.querySelectorAll('#mobileMenu a, [data-mobile-nav-close]').forEach(element => {
            element.addEventListener('click', () => {
                const instance = bootstrap.Offcanvas.getInstance(drawerElement);
                if (instance) instance.hide();
            });
        });
    }


    function setupScrollTop() {
        const button = document.querySelector('[data-scroll-top]');
        if (!button) return;

        const update = () => {
            button.classList.toggle('is-visible', window.scrollY > 520);
        };

        update();
        window.addEventListener('scroll', update, { passive: true });
        button.addEventListener('click', () => {
            const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            window.scrollTo({ top: 0, behavior: reduceMotion ? 'auto' : 'smooth' });
        });
    }

    function focusFirstValidationError() {
        const invalid = document.querySelector('.input-validation-error, [aria-invalid="true"]');
        if (!(invalid instanceof HTMLElement)) return;

        window.setTimeout(() => {
            try {
                invalid.focus({ preventScroll: true });
                invalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
            } catch {
                invalid.focus();
            }
        }, 80);
    }

    function setupSubmitFeedback() {
        document.addEventListener('submit', event => {
            const form = event.target;
            if (!(form instanceof HTMLFormElement)) return;
            if ((form.method || 'get').toLowerCase() !== 'post') return;

            window.setTimeout(() => {
                if (event.defaultPrevented || !navigator.onLine || !form.checkValidity()) return;
                if (form.matches('[data-no-submit-feedback]')) return;

                form.classList.add('is-submitting');
                form.setAttribute('aria-busy', 'true');

                const submitter = event.submitter;
                if (submitter instanceof HTMLElement) {
                    submitter.classList.add('is-submitting');
                    submitter.setAttribute('aria-busy', 'true');
                    if (form.id === 'saleForm' || form.classList.contains('payment-create-form')) {
                        submitter.disabled = true;
                        const status = document.createElement('p');status.className='save-status';status.setAttribute('role','status');status.textContent='Save ho raha hai. Meherbani karke intezar karein...';submitter.after(status);
                    }
                }
            }, 0);
        });
    }

    function improveResponsiveTables() {
        document.querySelectorAll('.table-responsive').forEach(wrapper => {
            if (!(wrapper instanceof HTMLElement)) return;
            if (wrapper.scrollWidth <= wrapper.clientWidth) return;

            if (!wrapper.hasAttribute('tabindex')) wrapper.setAttribute('tabindex', '0');
            if (!wrapper.hasAttribute('role')) wrapper.setAttribute('role', 'region');
            if (!wrapper.hasAttribute('aria-label')) wrapper.setAttribute('aria-label', 'Scrollable table');
        });
    }

    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        deferredInstallPrompt = event;
        setInstallButtonsVisible(true);
    });

    document.addEventListener('click', async event => {
        const button = event.target.closest('[data-pwa-install]');
        if (!button || !deferredInstallPrompt) return;

        button.disabled = true;
        try {
            deferredInstallPrompt.prompt();
            await deferredInstallPrompt.userChoice;
        } finally {
            deferredInstallPrompt = null;
            setInstallButtonsVisible(false);
            button.disabled = false;
        }
    });

    window.addEventListener('appinstalled', () => {
        deferredInstallPrompt = null;
        setInstallButtonsVisible(false);
    });

    function updateConnectivityUi() {
        const online = navigator.onLine;

        if (networkBanner) {
            networkBanner.classList.toggle('d-none', online);
        }

        document.querySelectorAll('form[method="post"], form[method="POST"]').forEach(form => {
            form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(button => {
                button.disabled = !online;
                if (!online) {
                    button.setAttribute('data-offline-disabled', 'true');
                    button.setAttribute('title', 'Internet connection is required to post this transaction.');
                } else if (button.getAttribute('data-offline-disabled') === 'true') {
                    button.removeAttribute('data-offline-disabled');
                    button.removeAttribute('title');
                }
            });
        });
    }

    window.addEventListener('online', updateConnectivityUi);
    window.addEventListener('offline', updateConnectivityUi);
    window.addEventListener('pageshow', () => {
        document.querySelectorAll('form.is-submitting').forEach(form => {
            form.querySelectorAll('.save-status').forEach(x=>x.remove());
            form.classList.remove('is-submitting');
            form.removeAttribute('aria-busy');
            form.querySelectorAll('.is-submitting').forEach(button => {
                button.classList.remove('is-submitting');
                button.removeAttribute('aria-busy');
            });
        });
        updateConnectivityUi();
    });

    document.addEventListener('DOMContentLoaded', () => {
        updateConnectivityUi();
        updateActiveNavigation();
        closeMobileDrawerOnNavigation();
        setupScrollTop();
        setupSubmitFeedback();
        improveResponsiveTables();
        focusFirstValidationError();
    });

    document.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        if ((form.method || 'get').toLowerCase() !== 'post') return;
        if (navigator.onLine) return;

        event.preventDefault();
        window.alert('You are offline. Reconnect to the internet before posting a sale, payment, or stock change.');
    });

    if ('serviceWorker' in navigator) {
        window.addEventListener('load', () => {
            navigator.serviceWorker.register('/service-worker.js', { scope: '/' })
                .catch(error => console.warn('PWA service worker registration failed.', error));
        });
    }
})();
