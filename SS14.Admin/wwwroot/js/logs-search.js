const datePicker = new DatePicker('datePicker', 'litepicker-close');

function formatLogTimesInBrowserZone() {
    const formatter = new Intl.DateTimeFormat(undefined, {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hourCycle: 'h23'
    });

    document.querySelectorAll('time.local-log-time[datetime]').forEach(element => {
        const date = new Date(element.dateTime);
        if (!Number.isNaN(date.getTime()))
            element.textContent = formatter.format(date);
    });
}

formatLogTimesInBrowserZone();

// Local design controls: experiment with page tokens without changing search state.
(() => {
    const workspace = document.getElementById('logs-workspace');
    const toggle = document.getElementById('logs-design-toggle');
    const tools = document.getElementById('logs-design-tools');
    const palette = document.getElementById('logs-palette');
    const density = document.getElementById('logs-density');
    if (!workspace || !toggle || !tools) return;

    const storedPalette = localStorage.getItem('logs-design-palette');
    const storedDensity = localStorage.getItem('logs-design-density');
    if (storedPalette) workspace.dataset.palette = storedPalette;
    if (storedDensity) workspace.dataset.density = storedDensity;
    if (palette) palette.value = storedPalette || 'command';
    if (density) density.value = storedDensity || 'compact';

    toggle.addEventListener('click', () => {
        const active = toggle.getAttribute('aria-pressed') !== 'true';
        toggle.setAttribute('aria-pressed', String(active));
        tools.hidden = !active;
        workspace.classList.toggle('logs-design-mode', active);
    });
    palette?.addEventListener('change', () => {
        workspace.dataset.palette = palette.value;
        localStorage.setItem('logs-design-palette', palette.value);
    });
    density?.addEventListener('change', () => {
        workspace.dataset.density = density.value;
        localStorage.setItem('logs-design-density', density.value);
    });
})();

$(document).ready(function() {
    const loading = document.getElementById('logs-loading');
    let navigationPending = false;

    function showLoading() {
        if (navigationPending || !loading) return;
        navigationPending = true;
        document.getElementById('logs-search-form')?.setAttribute('aria-busy', 'true');
        loading.classList.add('is-visible');
        loading.setAttribute('aria-hidden', 'false');
    }

    document.getElementById('logs-search-form')?.addEventListener('submit', showLoading);

    const update = context.update.bind(context);
    context.update = function() {
        showLoading();
        requestAnimationFrame(() => setTimeout(update, 30));
    };

    document.querySelectorAll('a[href*="pageIndex"], .logs-reset').forEach(link => {
        link.addEventListener('click', event => {
            if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            showLoading();
        });
    });

    const countSelect = document.getElementById('countselect');
    countSelect?.addEventListener('change', () => {
        context.parameters.set('countselect', countSelect.value);
        context.parameters.delete(context.parameterNames.PAGE_INDEX);
        context.update();
    });
});
