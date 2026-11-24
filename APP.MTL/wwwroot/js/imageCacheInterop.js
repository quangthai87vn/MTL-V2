/**
 * ImageCacheInterop.js
 * JS Interop cho image caching trong trình duyệt
 * Lưu trữ base64 data URI vào localStorage (cache cấp 1)
 * Fallback: IndexedDB cho ảnh lớn (>2MB localStorage limit)
 */

(function () {
    'use strict';

    var CACHE_KEY = 'mtl_image_cache_v1';
    var STORAGE_QUOTA_MB = 4.5;
    var MAX_LOCALSTORAGE_ITEM_MB = 2;

    // ── IndexedDB Setup ──────────────────────────────────────────
    var idb = null;
    var idbReady = null;

    function openIDB() {
        if (idb && idb.readyState === 'open') return Promise.resolve(idb);
        if (idbReady) return idbReady;

        idbReady = new Promise(function (resolve, reject) {
            var request = indexedDB.open('MTL_ImageCache', 1);
            request.onerror = function () { reject(request.error); };
            request.onsuccess = function () { idb = request.result; resolve(idb); };
            request.onupgradeneeded = function (e) {
                var db = e.target.result;
                if (!db.objectStoreNames.contains('images')) {
                    db.createObjectStore('images', { keyPath: 'url' });
                }
            };
        });
        return idbReady;
    }

    // ── Estimate storage usage ───────────────────────────────────
    function getStorageEstimate() {
        return navigator.storage && navigator.storage.estimate ? navigator.storage.estimate() : Promise.resolve({ usage: 0, quota: STORAGE_QUOTA_MB * 1024 * 1024 });
    }

    // ── localStorage helpers ─────────────────────────────────────
    function getCache() {
        try {
            var raw = localStorage.getItem(CACHE_KEY);
            return raw ? JSON.parse(raw) : {};
        } catch (e) {
            return {};
        }
    }

    function saveCache(cache) {
        try {
            localStorage.setItem(CACHE_KEY, JSON.stringify(cache));
            return true;
        } catch (e) {
            // quota exceeded - clear old entries
            if (e.name === 'QuotaExceededError' || e.message.includes('QuotaExceeded')) {
                trimLocalStorageCache();
                try { localStorage.setItem(CACHE_KEY, JSON.stringify(cache)); return true; } catch (_) {}
            }
            return false;
        }
    }

    function trimLocalStorageCache() {
        var cache = getCache();
        var entries = Object.entries(cache);
        if (entries.length < 5) return;
        entries.sort(function (a, b) { return (a[1].t || 0) - (b[1].t || 0); });
        var toRemove = entries.slice(0, Math.floor(entries.length / 2));
        toRemove.forEach(function (e) { delete cache[e[0]]; });
        saveCache(cache);
    }

    // ── Core get/set ────────────────────────────────────────────
    function setItem(url, base64Data, metadata) {
        var cache = getCache();
        cache[url] = { d: base64Data, t: Date.now(), s: metadata && metadata.size || 0 };
        saveCache(cache);
        // Also persist to IndexedDB
        openIDB().then(function (db) {
            try {
                var tx = db.transaction('images', 'readwrite');
                tx.objectStore('images').put({ url: url, data: base64Data, t: Date.now(), s: metadata && metadata.size || 0 });
            } catch (_) {}
        });
    }

    function getItem(url) {
        var cache = getCache();
        if (cache[url]) return cache[url].d;
        return null;
    }

    function removeItem(url) {
        var cache = getCache();
        delete cache[url];
        saveCache(cache);
        openIDB().then(function (db) {
            try {
                var tx = db.transaction('images', 'readwrite');
                tx.objectStore('images').delete(url);
            } catch (_) {}
        });
    }

    function getAllUrls() {
        return Object.keys(getCache());
    }

    function hasItem(url) {
        return Object.prototype.hasOwnProperty.call(getCache(), url);
    }

    // ── IndexedDB fallback get ──────────────────────────────────
    function getFromIDB(url) {
        return openIDB().then(function (db) {
            return new Promise(function (resolve) {
                try {
                    var tx = db.transaction('images', 'readonly');
                    var req = tx.objectStore('images').get(url);
                    req.onsuccess = function () { resolve(req.result ? req.result.data : null); };
                    req.onerror = function () { resolve(null); };
                } catch (e) { resolve(null); }
            });
        });
    }

    function setToIDB(url, base64Data, metadata) {
        openIDB().then(function (db) {
            try {
                var tx = db.transaction('images', 'readwrite');
                tx.objectStore('images').put({ url: url, data: base64Data, t: Date.now(), s: metadata && metadata.size || 0 });
            } catch (_) {}
        });
    }

    // ── Image download helpers ───────────────────────────────────
    function imgToBase64(src) {
        return new Promise(function (resolve, reject) {
            var img = new Image();
            img.crossOrigin = 'anonymous';
            img.onload = function () {
                try {
                    var canvas = document.createElement('canvas');
                    canvas.width = img.naturalWidth || img.width;
                    canvas.height = img.naturalHeight || img.height;
                    var ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0);
                    var mime = src.match(/\.(png|jpg|jpeg|gif|webp|svg)/i);
                    mime = mime ? 'image/' + (mime[1] === 'jpg' ? 'jpeg' : mime[1].toLowerCase()) : 'image/jpeg';
                    resolve({ base64: canvas.toDataURL(mime, 0.85), size: img.src.length, width: canvas.width, height: canvas.height });
                } catch (e) { reject(e); }
            };
            img.onerror = function (e) { reject(new Error('Image load failed: ' + src)); };
            img.src = src;
        });
    }

    // ── Public DotNet interop methods ───────────────────────────
    window.MTLImageCache = {
        /**
         * Lấy ảnh từ localStorage cache
         * @param {string} url
         * @returns {Promise<string|null>} base64 data URI hoặc null
         */
        getCached: function (url) {
            var cached = getItem(url);
            if (cached) return Promise.resolve(cached);
            return getFromIDB(url);
        },

        /**
         * Lưu ảnh (crossOrigin proxy nếu cần) vào localStorage + IndexedDB
         * @param {string} url
         * @param {string} crossOriginProxyPrefix - prefix URL server proxy (e.g. "/api/imageproxy?url=")
         * @returns {Promise<{data: string|null, fromCache: boolean, size: number}>}
         */
        cacheImage: function (url, crossOriginProxyPrefix) {
            return new Promise(function (resolve) {
                // 1. Check localStorage first
                var cached = getItem(url);
                if (cached) {
                    resolve({ data: cached, fromCache: true, size: cached.length });
                    return;
                }

                // 2. Check IndexedDB
                getFromIDB(url).then(function (idbData) {
                    if (idbData) {
                        setItem(url, idbData, {});
                        resolve({ data: idbData, fromCache: true, size: idbData.length });
                        return;
                    }

                    // 3. Download via CORS proxy
                    var imgSrc = (crossOriginProxyPrefix || '') + encodeURIComponent(url);
                    imgToBase64(imgSrc).then(function (result) {
                        setItem(url, result.base64, { size: result.size });
                        resolve({ data: result.base64, fromCache: false, size: result.size });
                    }).catch(function () {
                        resolve({ data: null, fromCache: false, size: 0 });
                    });
                });
            });
        },

        /**
         * Batch cache nhiều ảnh cùng lúc (parallel, có concurrency limit)
         * @param {string[]} urls
         * @param {string} proxyPrefix
         * @param {number} concurrency - số ảnh cache đồng thời (default 4)
         * @returns {Promise<Object>} map url → {data, fromCache, size}
         */
        cacheImagesBatch: function (urls, proxyPrefix, concurrency) {
            concurrency = concurrency || 4;
            var results = {};
            var pending = urls.slice();
            var active = 0;
            var queue = [];

            function startNext() {
                while (active < concurrency && queue.length > 0) {
                    var url = queue.shift();
                    active++;
                    window.MTLImageCache.cacheImage(url, proxyPrefix).then(function (r) {
                        results[url] = r;
                        active--;
                        startNext();
                    });
                }
            }

            queue.push.apply(queue, pending);
            startNext();

            return new Promise(function (resolve) {
                function check() {
                    if (queue.length === 0 && active === 0) {
                        resolve(results);
                    } else {
                        setTimeout(check, 50);
                    }
                }
                check();
            });
        },

        /**
         * Xóa một ảnh khỏi cache
         */
        removeCached: function (url) {
            removeItem(url);
            return Promise.resolve();
        },

        /**
         * Xóa toàn bộ cache
         */
        clearCache: function () {
            try { localStorage.removeItem(CACHE_KEY); } catch (_) {}
            openIDB().then(function (db) {
                try {
                    var tx = db.transaction('images', 'readwrite');
                    tx.objectStore('images').clear();
                } catch (_) {}
            });
            return Promise.resolve();
        },

        /**
         * Lấy số lượng ảnh đang cached
         */
        getCacheCount: function () {
            return Object.keys(getCache()).length;
        },

        /**
         * Kiểm tra ảnh có trong cache không
         */
        hasCached: function (url) {
            return hasItem(url);
        },

        /**
         * Lấy thông tin cache (count, estimated size)
         */
        getCacheInfo: function () {
            var cache = getCache();
            var total = 0;
            for (var k in cache) total += (cache[k].s || 0);
            return getStorageEstimate().then(function (est) {
                return {
                    count: Object.keys(cache).length,
                    estimatedBytes: total,
                    storageQuota: est.quota,
                    storageUsage: est.usage
                };
            });
        },

        /**
         * Proactive preload: cache ảnh trước khi cần hiển thị
         * Gọi khi user hover vào card hoặc khi scroll vào viewport
         */
        preloadImage: function (url, proxyPrefix) {
            if (!url || hasItem(url)) return Promise.resolve();
            return imgToBase64((proxyPrefix || '') + encodeURIComponent(url))
                .then(function (r) { setItem(url, r.base64, { size: r.size }); })
                .catch(function () {});
        }
    };
})();
