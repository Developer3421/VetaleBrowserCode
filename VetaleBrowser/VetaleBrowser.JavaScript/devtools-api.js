// VetaleBrowser DevTools JavaScript API
// Використовується для збору даних з WebView для DevTools

(function() {
    'use strict';
    
    window.VetaleDevTools = window.VetaleDevTools || {};
    
    // ===== DOM Elements Extraction =====
    VetaleDevTools.extractDomTree = function() {
        const elements = [];
        
        function traverseElement(element, path = '') {
            if (!element || element.nodeType !== 1) return;
            
            const tagName = element.tagName.toLowerCase();
            const currentPath = path ? `${path} > ${tagName}` : tagName;
            
            // Extract attributes
            const attributes = {};
            for (let i = 0; i < element.attributes.length; i++) {
                const attr = element.attributes[i];
                attributes[attr.name] = attr.value;
            }
            
            // Extract computed styles (only important ones)
            const computedStyle = window.getComputedStyle(element);
            const styles = {
                display: computedStyle.display,
                position: computedStyle.position,
                width: computedStyle.width,
                height: computedStyle.height,
                margin: computedStyle.margin,
                padding: computedStyle.padding,
                color: computedStyle.color,
                backgroundColor: computedStyle.backgroundColor,
                fontSize: computedStyle.fontSize,
                fontFamily: computedStyle.fontFamily
            };
            
            elements.push({
                elementPath: currentPath,
                tagName: tagName,
                innerHtml: element.innerHTML.substring(0, 500), // Limit size
                attributes: JSON.stringify(attributes),
                computedStyles: JSON.stringify(styles)
            });
            
            // Traverse children
            for (let i = 0; i < element.children.length; i++) {
                traverseElement(element.children[i], currentPath);
            }
        }
        
        traverseElement(document.documentElement);
        return elements;
    };
    
    // ===== Performance Data Collection =====
    VetaleDevTools.getPerformanceData = function() {
        const perf = window.performance;
        const navigation = perf.timing;
        const entries = perf.getEntriesByType('navigation')[0] || {};
        
        const data = {
            url: window.location.href,
            loadTime: navigation.loadEventEnd - navigation.navigationStart,
            domContentLoadedTime: navigation.domContentLoadedEventEnd - navigation.navigationStart,
            firstPaintTime: 0,
            memoryUsed: 0,
            resourceTimings: []
        };
        
        // First Paint
        const paintEntries = perf.getEntriesByType('paint');
        const firstPaint = paintEntries.find(e => e.name === 'first-paint');
        if (firstPaint) {
            data.firstPaintTime = firstPaint.startTime;
        }
        
        // Memory (if available)
        if (performance.memory) {
            data.memoryUsed = performance.memory.usedJSHeapSize;
        }
        
        // Resource timings
        const resources = perf.getEntriesByType('resource');
        data.resourceTimings = resources.map(r => ({
            name: r.name,
            type: r.initiatorType,
            duration: r.duration,
            size: r.transferSize || 0,
            startTime: r.startTime
        }));
        
        return data;
    };
    
    // ===== Page Resources Extraction =====
    VetaleDevTools.extractPageResources = function() {
        const resources = [];
        
        // Scripts
        document.querySelectorAll('script').forEach((script, index) => {
            if (script.src) {
                resources.push({
                    url: script.src,
                    type: 'script',
                    contentType: 'application/javascript',
                    content: null // External scripts won't have content
                });
            } else if (script.textContent) {
                resources.push({
                    url: `inline-script-${index}`,
                    type: 'script',
                    contentType: 'application/javascript',
                    content: script.textContent
                });
            }
        });
        
        // Stylesheets
        document.querySelectorAll('link[rel="stylesheet"]').forEach(link => {
            resources.push({
                url: link.href,
                type: 'stylesheet',
                contentType: 'text/css',
                content: null
            });
        });
        
        // Inline styles
        document.querySelectorAll('style').forEach((style, index) => {
            resources.push({
                url: `inline-style-${index}`,
                type: 'stylesheet',
                contentType: 'text/css',
                content: style.textContent
            });
        });
        
        // Images
        document.querySelectorAll('img').forEach(img => {
            resources.push({
                url: img.src,
                type: 'image',
                contentType: img.type || 'image/*',
                content: null
            });
        });
        
        return resources;
    };
    
    // ===== Storage Data Extraction =====
    VetaleDevTools.extractStorageData = function() {
        const storage = {
            localStorage: [],
            sessionStorage: [],
            cookies: []
        };
        
        // LocalStorage
        try {
            for (let i = 0; i < localStorage.length; i++) {
                const key = localStorage.key(i);
                if (key) {
                    storage.localStorage.push({
                        key: key,
                        value: localStorage.getItem(key),
                        domain: window.location.hostname
                    });
                }
            }
        } catch (e) {
            console.error('Cannot access localStorage:', e);
        }
        
        // SessionStorage
        try {
            for (let i = 0; i < sessionStorage.length; i++) {
                const key = sessionStorage.key(i);
                if (key) {
                    storage.sessionStorage.push({
                        key: key,
                        value: sessionStorage.getItem(key),
                        domain: window.location.hostname
                    });
                }
            }
        } catch (e) {
            console.error('Cannot access sessionStorage:', e);
        }
        
        // Cookies
        try {
            const cookies = document.cookie.split(';');
            cookies.forEach(cookie => {
                const parts = cookie.trim().split('=');
                if (parts.length >= 1) {
                    storage.cookies.push({
                        key: parts[0],
                        value: parts.slice(1).join('='),
                        domain: window.location.hostname
                    });
                }
            });
        } catch (e) {
            console.error('Cannot access cookies:', e);
        }
        
        return storage;
    };
    
    // ===== Source Code Extraction =====
    VetaleDevTools.extractSourceCode = function() {
        return {
            html: document.documentElement.outerHTML,
            scripts: Array.from(document.scripts).map((s, i) => ({
                id: i,
                src: s.src || null,
                content: s.src ? null : s.textContent,
                type: s.type || 'text/javascript'
            })),
            styles: Array.from(document.styleSheets).map((s, i) => {
                try {
                    const rules = Array.from(s.cssRules || []).map(r => r.cssText).join('\n');
                    return {
                        id: i,
                        href: s.href || null,
                        content: rules || null
                    };
                } catch (e) {
                    return {
                        id: i,
                        href: s.href || null,
                        content: null,
                        error: 'CORS or access denied'
                    };
                }
            })
        };
    };
    
    // ===== Complete Snapshot =====
    VetaleDevTools.captureCompleteSnapshot = function() {
        return {
            timestamp: new Date().toISOString(),
            url: window.location.href,
            title: document.title,
            domTree: VetaleDevTools.extractDomTree(),
            performance: VetaleDevTools.getPerformanceData(),
            resources: VetaleDevTools.extractPageResources(),
            storage: VetaleDevTools.extractStorageData(),
            sourceCode: VetaleDevTools.extractSourceCode()
        };
    };
    
    console.log('VetaleBrowser DevTools API initialized');
})();

