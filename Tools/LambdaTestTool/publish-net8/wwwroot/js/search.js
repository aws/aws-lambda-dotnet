(function () {
    var ENHANCED = 'data-sc-enhanced';
    var _scUpdating = false;

    var TARGETS = [
        { labels: ['Template', 'Config File'], placeholder: 'Search templates...' },
        { labels: ['Function'],                placeholder: 'Search functions...'  },
        { labels: ['Example Requests'],        placeholder: 'Search requests...'   }
    ];

    function findLabel(target) {
        return Array.from(document.querySelectorAll('label')).find(function (el) {
            var text = el.textContent.trim();
            return target.labels.some(function (l) { return text.startsWith(l); });
        }) || null;
    }

    function findSelectForLabel(label) {
        var parent = label.parentElement;
        if (!parent) return null;
        var children = Array.from(parent.children);
        var start = children.indexOf(label);
        for (var i = start + 1; i < children.length; i++) {
            if (children[i].tagName === 'SELECT') return children[i];
            var nested = children[i].querySelector('select');
            if (nested) return nested;
            if (children[i].tagName === 'LABEL') break;
        }
        return null;
    }

    function isPlaceholder(option) {
        return option.text.trim().startsWith('-') || option.value === '';
    }

    function selectedText(select) {
        var opt = select.options[select.selectedIndex];
        return (opt && !isPlaceholder(opt)) ? opt.text : '';
    }

    function highlightMatch(text, query) {
        if (!query) return document.createTextNode(text);
        var idx = text.toLowerCase().indexOf(query.toLowerCase());
        if (idx === -1) return document.createTextNode(text);
        var frag = document.createDocumentFragment();
        frag.appendChild(document.createTextNode(text.slice(0, idx)));
        var strong = document.createElement('strong');
        strong.className = 'sc-match';
        strong.textContent = text.slice(idx, idx + query.length);
        frag.appendChild(strong);
        frag.appendChild(document.createTextNode(text.slice(idx + query.length)));
        return frag;
    }

    function buildWidget(label, placeholder) {
        var select = findSelectForLabel(label);
        if (!select) {
            console.warn('[SC] buildWidget: no select found for label:', label.textContent.trim());
            return;
        }
        console.log('[SC] buildWidget:', label.textContent.trim(),
            '| options:', select.options.length,
            '| selected:', selectedText(select),
            '| inDOM:', document.contains(select));

        var wrapper = document.createElement('div');
        wrapper.className = 'sc-wrapper';
        wrapper.dataset.scLabel = label.textContent.trim();

        var inputRow = document.createElement('div');
        inputRow.className = 'sc-input-row';

        var input = document.createElement('input');
        input.type = 'text';
        input.className = 'sc-input form-control';
        input.placeholder = placeholder;
        input.setAttribute('autocomplete', 'off');
        input.setAttribute('spellcheck', 'false');
        input.value = selectedText(select);

        var clearBtn = document.createElement('button');
        clearBtn.type = 'button';
        clearBtn.className = 'sc-clear';
        clearBtn.setAttribute('tabindex', '-1');
        clearBtn.setAttribute('aria-label', 'Clear');
        clearBtn.innerHTML = '&times;';

        inputRow.appendChild(input);
        inputRow.appendChild(clearBtn);

        var panel = document.createElement('div');
        panel.className = 'sc-panel';

        wrapper.appendChild(inputRow);
        wrapper.appendChild(panel);

        _scUpdating = true;
        select.style.display = 'none';
        select.parentElement.insertBefore(wrapper, select);
        _scUpdating = false;

        var activeIndex = -1;
        var open = false;
        var visibleItems = [];

        function currentSelect() {
            var fresh = findSelectForLabel(label);
            if (!fresh) {
                console.warn('[SC] currentSelect: label detached, scanning DOM for', wrapper.dataset.scLabel);
                var newLabel = findLabel({ labels: [wrapper.dataset.scLabel] });
                if (newLabel && newLabel !== label) {
                    label = newLabel;
                    fresh = findSelectForLabel(label);
                }
            }
            if (fresh && fresh !== select) {
                console.log('[SC] currentSelect: re-anchored to new select, options:', fresh.options.length);
                select = fresh;
                select.style.display = 'none';
                select._scAttached = true;
                attachSelectObserver(select);
            }
            return select;
        }

        function buildPanel(query) {
            _scUpdating = true;
            try {
                panel.innerHTML = '';
                visibleItems = [];
                activeIndex = -1;

                var sel = currentSelect();
                var groups = Array.from(sel.querySelectorAll('optgroup'));
                var topOptions = Array.from(sel.querySelectorAll(':scope > option'));
                var q = (query || '').trim().toLowerCase();

                console.log('[SC] buildPanel:', wrapper.dataset.scLabel,
                    '| query:', JSON.stringify(q),
                    '| topOptions:', topOptions.length,
                    '| groups:', groups.length,
                    '| inDOM:', document.contains(sel));

                function makeItem(option) {
                    if (isPlaceholder(option)) return null;
                    var matches = !q
                        || option.text.toLowerCase().includes(q)
                        || option.value.toLowerCase().includes(q);
                    if (!matches) return null;

                    var item = document.createElement('div');
                    item.className = 'sc-item';
                    if (option.value === sel.value) item.classList.add('sc-item-selected');
                    item.appendChild(highlightMatch(option.text, q));
                    item.dataset.value = option.value;
                    item.dataset.text  = option.text;

                    item.addEventListener('mousedown', function (e) { e.preventDefault(); });
                    item.addEventListener('click', function () {
                        selectOption(option.value, option.text);
                    });

                    visibleItems.push(item);
                    return item;
                }

                topOptions.forEach(function (opt) {
                    var item = makeItem(opt);
                    if (item) panel.appendChild(item);
                });

                groups.forEach(function (group) {
                    var items = [];
                    Array.from(group.querySelectorAll('option')).forEach(function (opt) {
                        var item = makeItem(opt);
                        if (item) items.push(item);
                    });
                    if (!items.length) return;
                    var header = document.createElement('div');
                    header.className = 'sc-group-header';
                    header.textContent = group.label;
                    panel.appendChild(header);
                    items.forEach(function (item) { panel.appendChild(item); });
                });

                if (!visibleItems.length) {
                    var empty = document.createElement('div');
                    empty.className = 'sc-empty';
                    var realOptions = Array.from(sel.options).filter(function (o) { return !isPlaceholder(o); });
                    empty.textContent = realOptions.length ? 'No match' : 'Loading\u2026';
                    panel.appendChild(empty);
                }

                var selected = panel.querySelector('.sc-item-selected');
                if (selected) selected.scrollIntoView({ block: 'nearest' });
            } finally {
                _scUpdating = false;
            }
        }

        function setActive(index) {
            visibleItems.forEach(function (item) { item.classList.remove('sc-item-active'); });
            activeIndex = Math.max(0, Math.min(index, visibleItems.length - 1));
            if (visibleItems[activeIndex]) {
                visibleItems[activeIndex].classList.add('sc-item-active');
                visibleItems[activeIndex].scrollIntoView({ block: 'nearest' });
            }
        }

        function selectOption(value, text) {
            var sel = currentSelect();
            sel.value = value;
            sel.dispatchEvent(new Event('change', { bubbles: true }));
            input.value = text;
            closePanel();
        }

        function openPanel() {
            if (open) return;
            open = true;
            var curText = selectedText(currentSelect());
            buildPanel(input.value && input.value !== curText ? input.value : '');
            panel.style.display = 'block';
        }

        function closePanel() {
            if (!open) return;
            open = false;
            panel.style.display = 'none';
            input.value = selectedText(currentSelect());
            clearBtn.style.display = input.value ? '' : 'none';
        }

        input.addEventListener('focus', function () {
            input.select();
            openPanel();
        });

        input.addEventListener('input', function () {
            clearBtn.style.display = input.value ? '' : 'none';
            if (!open) { open = true; panel.style.display = 'block'; }
            buildPanel(input.value);
        });

        input.addEventListener('keydown', function (e) {
            if (!open && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {
                openPanel();
                return;
            }
            switch (e.key) {
                case 'ArrowDown':
                    e.preventDefault();
                    setActive(activeIndex < 0 ? 0 : activeIndex + 1);
                    break;
                case 'ArrowUp':
                    e.preventDefault();
                    setActive(activeIndex <= 0 ? 0 : activeIndex - 1);
                    break;
                case 'Enter':
                    e.preventDefault();
                    if (activeIndex >= 0 && visibleItems[activeIndex]) {
                        var item = visibleItems[activeIndex];
                        selectOption(item.dataset.value, item.dataset.text);
                    } else if (visibleItems.length === 1) {
                        selectOption(visibleItems[0].dataset.value, visibleItems[0].dataset.text);
                    }
                    break;
                case 'Escape':
                    e.preventDefault();
                    closePanel();
                    break;
                case 'Tab':
                    closePanel();
                    break;
            }
        });

        input.addEventListener('blur', function () {
            setTimeout(function () {
                if (!wrapper.contains(document.activeElement)) closePanel();
            }, 150);
        });

        clearBtn.addEventListener('click', function () {
            input.value = '';
            clearBtn.style.display = 'none';
            input.focus();
            buildPanel('');
            panel.style.display = 'block';
        });

        document.addEventListener('click', function (e) {
            if (!wrapper.contains(e.target)) closePanel();
        });

        clearBtn.style.display = input.value ? '' : 'none';

        function refresh() {
            var curText = selectedText(currentSelect());
            if (input.value !== curText && !open) {
                input.value = curText;
                clearBtn.style.display = curText ? '' : 'none';
            }
            if (open) buildPanel(input.value === curText ? '' : input.value);
        }

        wrapper._scRefresh = refresh;
        attachSelectObserver(select);

        function attachSelectObserver(sel) {
            sel._scAttached = true;
            sel._scRefresh  = refresh;
        }
    }

    function enhance() {
        console.log('[SC] enhance() called');
        TARGETS.forEach(function (target) {
            var label = findLabel(target);
            if (!label) {
                console.log('[SC]  ', target.labels.join('/'), '→ label NOT found');
                return;
            }
            if (label.getAttribute(ENHANCED)) {
                var hasWidget = label.parentElement
                    && label.parentElement.querySelector('.sc-wrapper');
                if (hasWidget) {
                    console.log('[SC]  ', target.labels.join('/'), '→ already enhanced, widget present');
                    return;
                }
                console.log('[SC]  ', target.labels.join('/'), '→ was enhanced but widget GONE, re-enhancing');
                label.removeAttribute(ENHANCED);
            }
            var sel = findSelectForLabel(label);
            if (!sel) {
                console.log('[SC]  ', target.labels.join('/'), '→ label found but NO select');
                return;
            }
            console.log('[SC]  ', target.labels.join('/'), '→ CREATING widget, select options:', sel.options.length);
            label.setAttribute(ENHANCED, '1');
            buildWidget(label, target.placeholder);
        });
    }

    var observer = new MutationObserver(function (mutations) {
        if (_scUpdating) return;
        var refreshWrappers = false;
        var selectsToRefresh = [];
        mutations.forEach(function (m) {
            if (m.type !== 'childList') return;
            if (m.target.closest && m.target.closest('.sc-wrapper')) return;
            if (m.target.tagName === 'SELECT' && m.target._scRefresh) {
                selectsToRefresh.push(m.target);
            } else {
                refreshWrappers = true;
            }
        });
        selectsToRefresh.forEach(function (sel) { sel._scRefresh(); });
        if (refreshWrappers) {
            document.querySelectorAll('.sc-wrapper').forEach(function (w) {
                if (w._scRefresh) w._scRefresh();
            });
            enhance();
        }
    });

    enhance();
    setTimeout(enhance, 500);
    setTimeout(enhance, 1500);
    setTimeout(enhance, 3000);
    observer.observe(document.body, { childList: true, subtree: true });
})();
