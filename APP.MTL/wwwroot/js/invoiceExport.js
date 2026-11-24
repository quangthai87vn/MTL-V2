window.mtlInvoicePrint = window.mtlInvoiceExport = {
	printBill: function (rootId, isBill) {
		var src = document.getElementById(rootId);
		if (!src) { alert('Không tìm thấy nội dung hóa đơn.'); return; }

		var clone = src.cloneNode(true);
		clone.removeAttribute('id');
		clone.querySelector('.invoice-toolbar')?.remove();

		var cssText = '';
		if (!isBill) {
			document.querySelectorAll('link[rel="stylesheet"]').forEach(l => cssText += l.outerHTML);
		}

		var billCss = [
			'@page{size:80mm auto;margin:0}',
			'html,body{margin:0!important;padding:0!important;background:#fff!important;-webkit-print-color-adjust:exact!important;print-color-adjust:exact!important}',
			'body{font-family:Roboto,Arial,sans-serif!important;color:#000!important}',
			'.invoice-root-bill{width:80mm!important;max-width:80mm!important;margin:0!important;padding:2mm 2.5mm!important;background:#fff!important;display:block!important;position:static!important;border:none!important;box-shadow:none!important}',
			'.invoice-root-bill *{color:#000!important;background:transparent!important;border-color:#000!important;box-shadow:none!important}',
			'.invoice-title-red{color:#b71c1c!important}',
			'img{max-width:100%!important;height:auto!important;display:block!important}',
			'.text-right{text-align:right!important}.text-center{text-align:center!important}.fw-bold{font-weight:700!important}'
		].join('');

		var a4Css = [
			'@page{size:A4 portrait;margin:0}',
			'html,body{margin:0!important;padding:0!important;background:#fff!important;-webkit-print-color-adjust:exact!important;print-color-adjust:exact!important}',
			'body{font-family:Roboto,Arial,sans-serif!important;color:#000!important}',
			'.invoice-root-a4{max-width:210mm!important;width:210mm!important;margin:0 auto!important;padding:0!important;background:#fff!important;border:2px solid #b71c1c!important;display:block!important;position:static!important;box-shadow:none!important}',
			'.invoice-root-a4 *{color:#000!important;background:transparent!important;border-color:#000!important;box-shadow:none!important}',
			'.invoice-title-red{color:#b71c1c!important}',
			'img{max-width:100%!important;height:auto!important;display:block!important}',
			'.text-right{text-align:right!important}.text-center{text-align:center!important}.fw-bold{font-weight:700!important}'
		].join('');

		var html = '<!DOCTYPE html><html><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width"/><title>Hóa đơn</title>' + cssText + '<style>' + (isBill ? billCss : a4Css) + '</style></head><body></body></html>';

		var ifW = isBill ? 80 : 794;
		var ifH = isBill ? 400 : 1123;

		var iframe = document.createElement('iframe');
		iframe.style.cssText = 'position:fixed;right:0;bottom:0;width:' + ifW + 'px;height:' + ifH + 'px;border:0;opacity:0;pointer-events:none;z-index:999999;';
		document.body.appendChild(iframe);

		var ifWin = iframe.contentWindow;
		var ifDoc = ifWin.document;
		ifDoc.open();
		ifDoc.write(html);
		ifDoc.close();
		ifDoc.body.appendChild(clone);

		function doPrint() {
			try { ifWin.focus(); ifWin.print(); }
			catch (e) { alert('Lỗi in: ' + e.message); }
			setTimeout(() => { try { document.body.removeChild(iframe); } catch (e) {} }, 4000);
		}
		setTimeout(doPrint, 400);
	}
};

window.mtlInvoiceExport = {
	print: function() { window.print(); },

	printLabel: function(elementId, pageSize, margin) {
		var src = document.getElementById(elementId);
		if (!src) { alert('Không tìm thấy nội dung in.'); return; }

		var clone = src.cloneNode(true);
		clone.removeAttribute('id');
		clone.querySelectorAll('.no-print, .product-print-toolbar, .mud-drawer, .mud-appbar, .mud-layout, .mud-main-content').forEach(el => el.remove());

		var mm2px = 96 / 25.4;
		var pagePxW, pagePxH;

		if (pageSize === 'A4') { pagePxW = 210 * mm2px; pagePxH = 297 * mm2px; }
		else if (pageSize === '72mm') { pagePxW = 72 * mm2px; pagePxH = 297 * mm2px; }
		else {
			var mmVal = parseFloat(pageSize);
			if (isNaN(mmVal)) { alert('Kích thước trang không hợp lệ: ' + pageSize); return; }
			pagePxW = mmVal * mm2px; pagePxH = 297 * mm2px;
		}

		var labelCss = [
			'@@page { size: ' + pageSize + ' auto; margin: ' + (margin || '0') + '; }',
			'html,body{margin:0!important;padding:0!important;background:#fff!important;-webkit-print-color-adjust:exact!important;print-color-adjust:exact!important}',
			'body{font-family:Roboto,Arial,sans-serif!important;color:#000!important}',
			'.tem-5030{width:' + pageSize + '!important;min-height:30mm!important;display:flex!important;flex-direction:column!important;justify-content:center!important;position:static!important;border:none!important;box-shadow:none!important;page-break-inside:avoid;overflow:visible!important}',
			'.tem-5030-inner{width:46mm!important;min-height:26mm!important;margin:2mm auto!important;padding:1mm!important;box-sizing:border-box!important;border:1px solid #000!important;display:flex!important;flex-direction:column!important;justify-content:center!important;align-items:center!important;text-align:center!important;overflow:visible!important}',
			'.tem-5030-name{font-size:8pt!important;font-weight:700!important;line-height:1.2!important;word-break:break-word!important;text-align:center!important;width:100%!important;padding:0 1mm!important;box-sizing:border-box!important;overflow:visible!important}',
			'.tem-5030-price{font-size:10pt!important;font-weight:800!important;color:#000!important;text-align:center!important;width:100%!important;padding:0.5mm 1mm!important;box-sizing:border-box!important;line-height:1!important;overflow:visible!important}',
			'.tem-5030-website{font-size:6pt!important;color:#666!important;text-align:center!important;width:100%!important;padding:0.3mm 1mm!important;box-sizing:border-box!important;line-height:1!important;overflow:visible!important}',
			'.tem-3522-sheet{display:flex!important;flex-direction:row!important;gap:2mm!important;width:72mm!important;margin:0 auto!important;padding:0!important;background:#fff!important;overflow:visible!important}',
			'.tem-3522-cell{width:35mm!important;min-height:21mm!important;flex-shrink:0!important;box-sizing:border-box!important;display:flex!important;flex-direction:column!important;font-family:Arial,sans-serif!important;background:#fff!important;overflow:visible!important}',
			'.tem-3522-info{flex:1!important;display:flex!important;flex-direction:column!important;justify-content:center!important;padding:0.5mm!important;box-sizing:border-box!important;text-align:center!important;line-height:1.2!important;overflow:visible!important}',
			'.tem-3522-name{font-size:6pt!important;font-weight:700!important;line-height:1.2!important;word-break:break-word!important;text-align:center!important;overflow:visible!important}',
			'.tem-3522-price{font-size:8pt!important;font-weight:800!important;text-align:center!important;box-sizing:border-box!important;line-height:1!important;overflow:visible!important}',
			'.thanhphan-root{padding:8mm!important;page-break-inside:avoid!important;overflow:visible!important}',
			'.thanhphan-root *{color:#000!important}',
			'.thanhphan-title{font-weight:700!important;font-size:14px!important;margin-bottom:4px!important}',
			'.thanhphan-table{width:100%!important;border-collapse:collapse!important;font-size:11px!important}',
			'.thanhphan-table th,.thanhphan-table td{border:1px solid #000!important;padding:3px 5px!important}',
			'.intem-root{padding:8mm!important;page-break-inside:avoid!important;overflow:visible!important}',
			'img{max-width:100%!important;height:auto!important;display:block!important}',
			'.text-right{text-align:right!important}.text-center{text-align:center!important}.fw-bold{font-weight:700!important}'
		].join('');

		var html = '<!DOCTYPE html><html><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width"/><title>Tem</title><style>' + labelCss + '</style></head><body></body></html>';

		var iframe = document.createElement('iframe');
		iframe.style.cssText = 'position:fixed;right:0;bottom:0;width:' + Math.round(pagePxW) + 'px;height:' + Math.round(pagePxH) + 'px;border:0;opacity:0;pointer-events:none;z-index:999999;';
		document.body.appendChild(iframe);

		var ifWin = iframe.contentWindow;
		var ifDoc = ifWin.document;
		ifDoc.open();
		ifDoc.write(html);
		ifDoc.close();
		ifDoc.body.appendChild(clone);

		function doPrint() {
			try { ifWin.focus(); ifWin.print(); }
			catch (e) { alert('Lỗi in: ' + e.message); }
			setTimeout(() => { try { document.body.removeChild(iframe); } catch (e) {} }, 4000);
		}
		setTimeout(doPrint, 400);
	},

	downloadPng: async function(elementId, fileName, pageSize) {
		console.log('[PNG] Start:', elementId, pageSize);
		var mm2px = 96 / 25.4;
		var pagePxW, pagePxH;

		if (pageSize === 'A4') { pagePxW = 210 * mm2px; pagePxH = 297 * mm2px; }
		else if (pageSize === '72mm') { pagePxW = 72 * mm2px; pagePxH = 297 * mm2px; }
		else {
			var mmVal = parseFloat(pageSize);
			if (isNaN(mmVal)) { console.error('Invalid pageSize:', pageSize); return false; }
			pagePxW = mmVal * mm2px; pagePxH = 297 * mm2px;
		}

		var src = document.getElementById(elementId);
		if (!src) { console.error('Element not found:', elementId); return false; }

		var clone = src.cloneNode(true);
		clone.removeAttribute('id');
		clone.classList.add('invoice--export');
		clone.querySelectorAll('.no-print, .product-print-toolbar, .mud-drawer, .mud-appbar, .mud-layout, .mud-main-content').forEach(el => el.remove());

		// FORCE LOGO VISIBLE — đặt style trực tiếp lấy precedence cao nhất
		var logoImg = clone.querySelector('.invoice-a4-logo-img');
		if (logoImg) {
			logoImg.style.setProperty('display', 'block', 'important');
			logoImg.style.setProperty('opacity', '1', 'important');
			logoImg.style.setProperty('visibility', 'visible', 'important');
			logoImg.style.setProperty('max-width', '148px', 'important');
			logoImg.style.setProperty('max-height', '88px', 'important');
			// Force crossOrigin nếu ảnh là local
			if (logoImg.src && logoImg.src.includes(window.location.origin)) {
				logoImg.crossOrigin = 'anonymous';
			}
			console.log('[PDF] Logo found — src:', logoImg.src.substring(0, 60), '| complete:', logoImg.complete);
		} else {
			console.warn('[PDF] Logo NOT found in clone');
		}

		// Inject comprehensive CSS
		var css = `
			/* Reset & Base */
			.invoice-root-a4, .invoice-root-bill {
				box-sizing: border-box !important;
				margin: 16px auto !important;
				padding: 0 !important;
				background: #fff !important;
				display: flex !important;
				flex-direction: column !important;
				overflow: visible !important;
				font-family: Roboto, Arial, sans-serif !important;
				color: #000 !important;
			}
			.invoice-root-a4 {
				max-width: 210mm !important;
				width: 210mm !important;
				border: 2px solid #b71c1c !important;
				border-radius: 2px !important;
				box-shadow: 0 4px 20px rgba(0,0,0,.12) !important;
				font-size: 12px !important;
			}
			.invoice-root-bill {
				max-width: 80mm !important;
				padding: 4mm 3mm !important;
				border: 1px solid #ccc !important;
				font-size: 11px !important;
			}
			.invoice-root-a4 *, .invoice-root-bill * {
				box-sizing: border-box !important;
				color: #000 !important;
				background: transparent !important;
				border-color: #000 !important;
				box-shadow: none !important;
				font-size: inherit !important;
				line-height: inherit !important;
			}

			/* Header */
			.invoice-a4-header-band {
				width: 100% !important;
				height: 5px !important;
				background: linear-gradient(90deg, #b71c1c 0%, #e53935 40%, #ef5350 70%, #b71c1c 100%) !important;
				flex-shrink: 0 !important;
			}
			.invoice-a4-header {
				padding: 8px 14px 6px !important;
				border-bottom: 2px solid #b71c1c !important;
				background: #fff !important;
				flex-shrink: 0 !important;
			}
			.invoice-a4-hdr-grid {
				display: grid !important;
				grid-template-columns: 148px minmax(0, 1fr) 200px !important;
				gap: 12px !important;
				align-items: center !important;
			}
			.invoice-co-name {
				font-size: 17px !important;
				font-weight: 800 !important;
				color: #b71c1c !important;
				text-transform: uppercase !important;
				margin: 0 0 4px !important;
				text-align: left !important;
			}
			.invoice-company-block {
				font-size: 11px !important;
				line-height: 1.4 !important;
				color: #333 !important;
				text-align: left !important;
			}
			.invoice-hd-box {
				width: 100% !important;
				max-width: 220px !important;
				margin-left: auto !important;
				text-align: right !important;
			}
			.invoice-hd-title {
				font-size: 20px !important;
				font-weight: 800 !important;
				color: #b71c1c !important;
				text-transform: uppercase !important;
				margin: 0 0 6px !important;
				padding-bottom: 6px !important;
				border-bottom: 1px solid #e0e0e0 !important;
				text-align: right !important;
			}
			.invoice-hd-meta {
				font-size: 12px !important;
				line-height: 1.45 !important;
				text-align: right !important;
			}

			/* Table */
			.invoice-table {
				width: 100% !important;
				border-collapse: collapse !important;
				margin: 4px 14px 0 !important;
			}
			.invoice-table th, .invoice-table td {
				border: none !important;
				border-bottom: 1px solid #e0e0e0 !important;
				padding: 5px 7px !important;
				vertical-align: top !important;
				font-size: 11.5px !important;
				text-align: inherit !important;
			}
			.invoice-table thead tr th {
				background: #b71c1c !important;
				color: #fff !important;
				font-size: 11px !important;
				font-weight: 700 !important;
				text-transform: uppercase !important;
				padding: 6px 7px !important;
			}

			/* Payment section */
			.invoice-a4-bottom {
				flex-shrink: 0 !important;
				padding: 0 14px !important;
				margin-top: 2px !important;
			}
			.invoice-section-pay {
				background: #b71c1c !important;
				color: #fff !important;
				font-size: 12px !important;
				font-weight: 800 !important;
				text-transform: uppercase !important;
				text-align: center !important;
				padding: 6px 12px !important;
				border-radius: 2px 2px 0 0 !important;
				margin: 0 !important;
			}
			.invoice-a4-pay-body {
				display: grid !important;
				grid-template-columns: minmax(108px, 22%) minmax(0, 1.1fr) minmax(0, 1fr) !important;
				gap: 12px 14px !important;
				align-items: start !important;
				padding: 8px 0 5px !important;
				border-bottom: 1px solid #e0e0e0 !important;
			}
			.invoice-a4-pay-qr img {
				width: 110px !important;
				height: 110px !important;
				display: block !important;
			}
			.invoice-a4-pay-bank {
				font-size: 11.5px !important;
				line-height: 1.5 !important;
			}
			.invoice-bank-name {
				font-size: 13px !important;
				font-weight: 800 !important;
				color: #b71c1c !important;
				margin-bottom: 3px !important;
			}
			.invoice-sum-grid {
				width: 100% !important;
				border-collapse: collapse !important;
				font-size: 15px !important;
			}
			.invoice-sum-grid td {
				padding: 4px 0 4px 8px !important;
				font-size: 15px !important;
				vertical-align: baseline !important;
			}
			.invoice-sum-total-row td {
				color: #b71c1c !important;
				font-weight: 800 !important;
				font-size: 15px !important;
			}
			.invoice-sum-value {
				font-weight: 700 !important;
				text-align: right !important;
				white-space: nowrap !important;
				font-variant-numeric: tabular-nums !important;
			}

			/* Signature */
			.invoice-a4-signatures {
				width: 100% !important;
				border-collapse: collapse !important;
				margin-top: 8px !important;
				table-layout: fixed !important;
			}
			.invoice-a4-signatures td {
				padding: 8px 6px !important;
				text-align: center !important;
				vertical-align: top !important;
				font-size: 11px !important;
				border: 1px solid #e0e0e0 !important;
			}
			.invoice-sig-title {
				font-size: 11.5px !important;
				font-weight: 700 !important;
				color: #333 !important;
				display: block !important;
				margin-bottom: 2px !important;
			}
			.invoice-sig-sub {
				font-size: 10px !important;
				font-weight: 400 !important;
				color: #888 !important;
				display: block !important;
			}
			.invoice-a4-sig-space {
				display: block !important;
				min-height: 48px !important;
			}

			/* Footer */
			.invoice-a4-footer {
				margin-top: 8px !important;
				padding: 6px 0 8px !important;
				font-size: 10px !important;
				color: #999 !important;
				text-align: center !important;
				font-style: italic !important;
				border-top: 1px solid #eee !important;
			}

			/* Utility */
			.text-right { text-align: right !important; }
			.text-center { text-align: center !important; }
			.fw-bold { font-weight: 700 !important; }
			img { max-width: 100% !important; height: auto !important; display: block !important; opacity: 1 !important; visibility: visible !important; }
			.invoice-a4-logo-img { max-width: 148px !important; max-height: 88px !important; display: block !important; object-fit: contain !important; }
			.invoice-title-red { color: #b71c1c !important; font-weight: 700 !important; }
			hr { border: none !important; border-top: 1px dashed #333 !important; margin: 6px 0 !important; height: 1px !important; width: 100% !important; }
		`;

		var styleEl = document.createElement('style');
		styleEl.textContent = css;
		clone.insertBefore(styleEl, clone.firstChild);

		// Force images visible
		clone.querySelectorAll('img').forEach(function(img) {
			img.style.display = 'block';
			img.style.opacity = '1';
			img.style.visibility = 'visible';
			if (img.src && img.src.startsWith('http') && img.src.includes(window.location.origin)) {
				img.crossOrigin = 'anonymous';
			}
		});

		// Build iframe — inject all CSS inline
		var allCSS = css;
		try {
			document.querySelectorAll('link[rel="stylesheet"]').forEach(function(link) {
				if (link.sheet && link.sheet.cssRules) {
					for (var i = 0; i < link.sheet.cssRules.length; i++) {
						var rule = link.sheet.cssRules[i];
						if (rule.cssText) allCSS += rule.cssText + '\n';
					}
				}
			});
		} catch (e) {}

		var iframeHtml = '<!DOCTYPE html><html><head><meta charset="utf-8"/><meta name="viewport" content="width=' + pagePxW + 'px"/><style>' + allCSS + '</style></head><body style="margin:0;padding:0;background:#fff;"></body></html>';

		var iframe = document.createElement('iframe');
		iframe.style.cssText = 'position:absolute;left:-9999px;top:0;width:' + Math.round(pagePxW) + 'px;height:' + Math.round(pagePxH) + 'px;border:0;z-index:999999;';
		document.body.appendChild(iframe);

		var ifWin = iframe.contentWindow;
		var ifDoc = ifWin.document;
		ifDoc.open(); ifDoc.write(iframeHtml); ifDoc.close();

		await new Promise(r => setTimeout(r, 500));
		ifDoc.body.appendChild(clone);

		console.log('[PNG] Clone children:', clone.children.length, '| imgs:', clone.querySelectorAll('img').length, '| text:', clone.textContent?.trim().slice(0, 100) || '(empty)');

		// Wait
		await Promise.all([
			ifWin.document.fonts.ready,
			new Promise(function(resolve) {
				var imgs = clone.querySelectorAll('img');
				if (imgs.length === 0) return resolve();
				var loaded = 0;
				imgs.forEach(function(img) {
					if (img.complete) {
						loaded++;
						if (loaded === imgs.length) resolve();
					} else {
						img.onload = img.onerror = function() {
							loaded++;
							if (loaded === imgs.length) resolve();
						};
					}
				});
				if (loaded === imgs.length) resolve();
				setTimeout(resolve, 3000); // fallback
			}),
			new Promise(r => setTimeout(r, 2000))
		]);

		console.log('[PNG] Iframe size:', ifDoc.body.offsetWidth, 'x', ifDoc.body.offsetHeight, '| display:', getComputedStyle(clone).display, 'opacity:', getComputedStyle(clone).opacity);

		try {
			if (typeof html2canvas === 'undefined') { alert('html2canvas missing'); return false; }

			var canvas = await html2canvas(ifDoc.body, {
				scale: 2, useCORS: true, allowTaint: false,
				backgroundColor: '#ffffff', width: Math.round(pagePxW), logging: true,
				onclone: function(doc) {
					doc.querySelectorAll('img').forEach(img => {
						if (!img.complete || img.naturalWidth === 0) {
							img.src = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';
							img.style.width = '100%';
							img.style.height = 'auto';
						}
					});
				}
			});

			if (!canvas || canvas.width === 0) { alert('Canvas empty'); return false; }

			var a = document.createElement('a');
			a.download = (fileName || 'hoadon') + '.png';
			a.href = canvas.toDataURL('image/png');
			a.click();
			console.log('[PNG] Success — size:', canvas.width, 'x', canvas.height);
			return true;
		} catch (e) {
			console.error('PNG error:', e);
			alert('PNG error: ' + e.message);
			return false;
		} finally {
			setTimeout(() => { try { document.body.removeChild(iframe); } catch(e) {} }, 1500);
		}
	},

	downloadPdf: async function(elementId, fileName, pageSize) {
		console.log('[PDF] Start:', elementId, pageSize);
		var mm2px = 96 / 25.4;
		var pagePxW, pagePxH, pdfW, pdfH;

		if (pageSize === 'A4') { pagePxW = 210 * mm2px; pagePxH = 297 * mm2px; pdfW = 210; pdfH = 297; }
		else if (pageSize === '72mm') { pagePxW = 72 * mm2px; pagePxH = 297 * mm2px; pdfW = 72; pdfH = 297; }
		else {
			var mmVal = parseFloat(pageSize);
			if (isNaN(mmVal)) { console.error('Invalid pageSize:', pageSize); return false; }
			pagePxW = mmVal * mm2px; pagePxH = 297 * mm2px; pdfW = mmVal; pdfH = 297;
		}

		var src = document.getElementById(elementId);
		if (!src) return false;

		var clone = src.cloneNode(true);
		clone.removeAttribute('id');
		clone.querySelectorAll('.no-print, .product-print-toolbar, .mud-drawer, .mud-appbar, .mud-layout, .mud-main-content').forEach(el => el.remove());

		// FORCE LOGO VISIBLE — set inline style với !important
		var logoImg = clone.querySelector('.invoice-a4-logo-img');
		if (logoImg) {
			logoImg.style.setProperty('display', 'block', 'important');
			logoImg.style.setProperty('opacity', '1', 'important');
			logoImg.style.setProperty('visibility', 'visible', 'important');
			logoImg.style.setProperty('max-width', '148px', 'important');
			logoImg.style.setProperty('max-height', '88px', 'important');
			if (logoImg.src && logoImg.src.includes(window.location.origin)) {
				logoImg.crossOrigin = 'anonymous';
			}
			console.log('[PDF] Logo found — src:', logoImg.src.substring(0, 60), '| complete:', logoImg.complete, '| naturalWidth:', logoImg.naturalWidth);
		} else {
			console.warn('[PDF] Logo NOT found in clone');
		}

		var css = `
			.invoice-root-a4{max-width:210mm!important;width:210mm!important;margin:16px auto!important;padding:0!important;background:#fff!important;border:2px solid #b71c1c!important;display:flex!important;flex-direction:column!important;box-shadow:none!important}
			.invoice-root-bill{max-width:80mm!important;margin:16px auto!important;padding:4mm 3mm!important;background:#fff!important;display:block!important;border:1px solid #ccc!important}
			.invoice-root-a4 *, .invoice-root-bill *{color:#000!important;background:transparent!important;box-sizing:border-box!important}
			.invoice-a4-header-band{height:5px!important;background:linear-gradient(90deg,#b71c1c,#e53935,#ef5350,#b71c1c)!important}
			.invoice-a4-header{padding:8px 14px 6px!important;border-bottom:2px solid #b71c1c!important}
			.invoice-co-name{font-size:17px!important;font-weight:800!important;color:#b71c1c!important;text-align:left!important;margin:0 0 4px!important}
			.invoice-hd-title{font-size:20px!important;font-weight:800!important;color:#b71c1c!important;text-align:right!important;margin:0 0 6px!important;padding-bottom:6px!important;border-bottom:1px solid #e0e0e0!important}
			.invoice-table{width:100%!important;border-collapse:collapse!important;margin:4px 14px 0!important}
			.invoice-table th, .invoice-table td{border:none!important;border-bottom:1px solid #e0e0e0!important;padding:5px 7px!important;vertical-align:top!important;font-size:11.5px!important;color:#000!important}
			.invoice-table thead tr th{background:#b71c1c!important;color:#fff!important;font-size:11px!important;font-weight:700!important;text-transform:uppercase!important}
			.invoice-section-pay{background:#b71c1c!important;color:#fff!important;font-size:12px!important;font-weight:800!important;text-align:center!important;padding:6px 12px!important;margin:0!important;border-radius:2px 2px 0 0!important}
			.invoice-sum-grid{width:100%!important;border-collapse:collapse!important;font-size:15px!important}
			.invoice-sum-total-row td{color:#b71c1c!important;font-weight:800!important}
			img{max-width:100%!important;height:auto!important;display:block!important;opacity:1!important;visibility:visible!important}
			.text-right{text-align:right!important}.text-center{text-align:center!important}.fw-bold{font-weight:700!important}
			.invoice-a4-logo-img{max-width:148px!important;max-height:88px!important;display:block!important}
			.invoice-a4-customer, .invoice-a4-customer > div, .invoice-a4-customer-info, .invoice-company-block{text-align:left!important}
		`;

		try {
			document.querySelectorAll('link[rel="stylesheet"]').forEach(link => {
				if (link.sheet && link.sheet.cssRules) {
					for (var i = 0; i < link.sheet.cssRules.length; i++) {
						var rule = link.sheet.cssRules[i];
						if (rule.cssText) css += rule.cssText + '\n';
					}
				}
			});
		} catch (e) {}

		var iframeHtml = '<!DOCTYPE html><html><head><meta charset="utf-8"/><meta name="viewport" content="width=' + pagePxW + 'px"/><style>' + css + '</style></head><body style="margin:0;padding:0;background:#fff;"></body></html>';

		var iframe = document.createElement('iframe');
		iframe.style.cssText = 'position:absolute;left:-9999px;top:0;width:' + Math.round(pagePxW) + 'px;height:' + Math.round(pagePxH) + 'px;border:0;z-index:999999;';
		document.body.appendChild(iframe);

		var ifWin = iframe.contentWindow, ifDoc = ifWin.document;
		ifDoc.open(); ifDoc.write(iframeHtml); ifDoc.close();

		await new Promise(r => setTimeout(r, 500));
		ifDoc.body.appendChild(clone);

		console.log('[PDF] Clone children:', clone.children.length, '| imgs:', clone.querySelectorAll('img').length, '| text:', clone.textContent?.trim().slice(0, 100) || '(empty)');

		// Kiểm tra logo sau khi append
		var logoAfter = clone.querySelector('.invoice-a4-logo-img');
		if (logoAfter) {
			var logoStyle = getComputedStyle(logoAfter);
			console.log('[PDF] Logo after append — display:', logoStyle.display, 'opacity:', logoStyle.opacity, 'visible:', logoStyle.visibility, 'naturalWidth:', logoAfter.naturalWidth);
		}

		await Promise.all([
			ifWin.document.fonts.ready,
			new Promise(function(resolve) {
				var imgs = clone.querySelectorAll('img');
				if (imgs.length === 0) return resolve();
				var loaded = 0;
				imgs.forEach(function(img) {
					if (img.complete) {
						loaded++;
						if (loaded === imgs.length) resolve();
					} else {
						img.onload = img.onerror = function() {
							loaded++;
							if (loaded === imgs.length) resolve();
						};
					}
				});
				if (loaded === imgs.length) resolve();
				setTimeout(resolve, 3000);
			}),
			new Promise(r => setTimeout(r, 2000))
		]);

		console.log('[PDF] Iframe size:', ifDoc.body.offsetWidth, 'x', ifDoc.body.offsetHeight);

		try {
			if (typeof window.html2pdf !== 'undefined') {
				var opt = {
					margin: 0, filename: (fileName || 'hoadon') + '.pdf',
					image: { type: 'jpeg', quality: 0.95 },
					html2canvas: { scale: 2, useCORS: true, allowTaint: false, backgroundColor: '#ffffff', width: Math.round(pagePxW), logging: true },
					jsPDF: { unit: 'mm', format: [pdfW, pdfH], orientation: 'portrait' }
				};
				await new Promise((res, rej) => window.html2pdf().set(opt).from(ifDoc.body).save().then(res).catch(rej));
				setTimeout(() => { try { document.body.removeChild(iframe); } catch(e) {} }, 1500);
				console.log('[PDF] Success');
				return true;
			}
			else if (typeof window.jspdf !== 'undefined' && window.jspdf.jsPDF) {
				var { jsPDF } = window.jspdf;
				var canvas = await html2canvas(ifDoc.body, { scale: 2, useCORS: true, allowTaint: false, backgroundColor: '#ffffff', width: Math.round(pagePxW), logging: true });
				if (!canvas || canvas.width === 0) { alert('Canvas empty'); return false; }
				var pdf = new jsPDF({ unit: 'mm', format: [pdfW, pdfH], orientation: 'portrait' });
				var pw = pdf.internal.pageSize.getWidth(), ph = pdf.internal.pageSize.getHeight();
				var imgH = (canvas.height * pw) / canvas.width;
				var y = 0;
				while (y < imgH) { if (y > 0) pdf.addPage(); pdf.addImage(canvas.toDataURL('image/png'), 'PNG', 0, -y, pw, imgH); y += ph; }
				pdf.save((fileName || 'hoadon') + '.pdf');
				setTimeout(() => { try { document.body.removeChild(iframe); } catch(e) {} }, 1500);
				console.log('[PDF] Success (jsPDF)');
				return true;
			}
			else throw new Error('html2pdf/jsPDF missing');
		} catch (ex) {
			console.error('PDF error:', ex);
			alert('PDF error: ' + ex.message);
			return false;
		}
	},

	downloadTextFile: function(content, fileName, mime) {
		const blob = new Blob([content], { type: mime || 'text/plain;charset=utf-8' });
		const url = URL.createObjectURL(blob);
		const a = document.createElement('a');
		a.href = url; a.download = fileName; a.click();
		URL.revokeObjectURL(url);
	},

	loadPrintAndPrint: function(url) {
		const width = 800, height = 600;
		const left = Math.round((screen.width/2) - (width/2));
		const top = Math.round((screen.height/2) - (height/2));
		const features = `width=${width},height=${height},top=${top},left=${left},scrollbars=yes,resizable=yes`;
		const win = window.open(url, '_blank', features);
		if (!win) { alert('Popup blocked!'); return; }
		win.focus();
	}
};
