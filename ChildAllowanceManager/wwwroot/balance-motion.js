window.AllowanceMotion = {
    // Drops short-lived decorative pieces into a card, then removes them. Purely visual:
    // the balance text is the record, so nothing here is announced or focusable.
    scatter: function (card, kind, count, build) {
        const layer = document.createElement('div');
        layer.className = `card-fx card-fx-${kind}`;
        layer.setAttribute('aria-hidden', 'true');
        for (let index = 0; index < count; index++) {
            const piece = build(index);
            piece.style.setProperty('--fx-x', `${8 + Math.random() * 84}%`);
            piece.style.setProperty('--fx-turn', `${Math.round((Math.random() - .5) * 540)}deg`);
            piece.style.setProperty('--fx-delay', `${index * 55}ms`);
            layer.appendChild(piece);
        }
        card.appendChild(layer);
        window.setTimeout(() => layer.remove(), 1900);
    },

    coins: function (card, direction) {
        window.AllowanceMotion.scatter(card, `coins-${direction}`, 6, () => {
            const coin = document.createElement('img');
            coin.src = 'art/coin.png';
            coin.alt = '';
            coin.className = 'fx-coin';
            return coin;
        });
        const buddy = card.querySelector('.child-buddy');
        if (!buddy) return;
        buddy.classList.remove('buddy-hop', 'buddy-shake');
        void buddy.offsetWidth;
        buddy.classList.add(direction === 'in' ? 'buddy-hop' : 'buddy-shake');
        window.setTimeout(() => buddy.classList.remove('buddy-hop', 'buddy-shake'), 900);
    },

    confetti: function (card) {
        window.AllowanceMotion.scatter(card, 'confetti', 18, (index) => {
            const piece = document.createElement('span');
            piece.className = `fx-confetti fx-confetti-${index % 5}`;
            return piece;
        });
    },

    animateBalances: function () {
        const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
        const formatter = new Intl.NumberFormat('en-GB', {
            style: 'currency',
            currency: 'GBP',
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });

        if (!reduced) {
            document.querySelectorAll('.child-birthday:not([data-confetti-done])').forEach((card) => {
                card.dataset.confettiDone = 'true';
                window.AllowanceMotion.confetti(card);
            });
        }

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
                const card = element.closest('.child-card');
                if (card && !reduced && (direction === 'in' || direction === 'out'))
                    window.AllowanceMotion.coins(card, direction);
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
