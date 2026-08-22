window.AllowanceMotion = {
    animateBalances: function () {
        const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
        const formatter = new Intl.NumberFormat('en-GB', {
            style: 'currency',
            currency: 'GBP',
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });

        document.querySelectorAll('[data-balance-value]').forEach((element) => {
            const target = Number(element.dataset.balanceValue);
            const previous = element.dataset.motionPrevious === undefined
                ? 0
                : Number(element.dataset.motionPrevious);
            const version = element.dataset.balanceMotionVersion;

            if (!Number.isFinite(target)) return;

            if (version && version !== element.dataset.motionApplied) {
                element.classList.remove('balance-motion-in', 'balance-motion-out');
                void element.offsetWidth;
                const direction = element.dataset.balanceMotionDirection;
                if (direction === 'in' || direction === 'out')
                    element.classList.add(`balance-motion-${direction}`);
                element.dataset.motionApplied = version;
                window.setTimeout(() => element.classList.remove('balance-motion-in', 'balance-motion-out'), 160);
            }

            element.dataset.motionPrevious = String(target);
            if (reduced || previous === target) {
                element.textContent = formatter.format(target);
                return;
            }

            const started = performance.now();
            const duration = 280;
            const render = (now) => {
                const progress = Math.min(1, (now - started) / duration);
                const eased = 1 - Math.pow(1 - progress, 3);
                element.textContent = formatter.format(previous + ((target - previous) * eased));
                if (progress < 1) window.requestAnimationFrame(render);
                else element.textContent = formatter.format(target);
            };
            window.requestAnimationFrame(render);
        });
    }
};
