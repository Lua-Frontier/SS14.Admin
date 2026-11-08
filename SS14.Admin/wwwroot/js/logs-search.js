const datePicker = new DatePicker('datePicker', 'litepicker-close');

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
