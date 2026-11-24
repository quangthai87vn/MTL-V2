/**
 * Xuất Excel (SheetJS) — dùng chung: exportToExcel(fileNameBase, headers, rows)
 * rows: mảng các dòng, mỗi dòng là mảng giá trị (chuỗi/số)
 */
(function () {
	function cellValue(c) {
		if (c === null || c === undefined) return '';
		if (typeof c === 'number' && isNaN(c)) return '';
		return c;
	}

	window.exportToExcel = function (fileNameBase, headers, rows) {
		if (typeof XLSX === 'undefined') {
			console.error('SheetJS (XLSX) chưa được tải.');
			return;
		}
		var name = (fileNameBase || 'export').toString().replace(/[\\/:*?"<>|]/g, '_');
		var hdr = (headers || []).map(cellValue);
		var body = (rows || []).map(function (r) {
			return (r || []).map(cellValue);
		});
		var aoa = [hdr].concat(body);
		var ws = XLSX.utils.aoa_to_sheet(aoa);
		var wb = XLSX.utils.book_new();
		XLSX.utils.book_append_sheet(wb, ws, 'Sheet1');
		XLSX.writeFile(wb, name + '.xlsx');
	};

	/**
	 * Hóa đơn: 2 sheet — Tổng quan + Chi tiết
	 * summaryRows: [ ["Nhãn", "Giá trị"], ... ]
	 * detailHeaders: [ "STT", "Tên SP", ... ]
	 * detailRows: [ [1, "SP", ...], ... ]
	 */
	window.exportInvoiceToExcel = function (fileNameBase, summaryRows, detailHeaders, detailRows) {
		if (typeof XLSX === 'undefined') {
			console.error('SheetJS (XLSX) chưa được tải.');
			return;
		}
		var name = (fileNameBase || 'HoaDon').toString().replace(/[\\/:*?"<>|]/g, '_');
		var sum = (summaryRows || []).map(function (r) {
			return (r || []).map(cellValue);
		});
		var dh = (detailHeaders || []).map(cellValue);
		var dr = (detailRows || []).map(function (r) {
			return (r || []).map(cellValue);
		});
		var wb = XLSX.utils.book_new();
		var ws1 = XLSX.utils.aoa_to_sheet(sum);
		XLSX.utils.book_append_sheet(wb, ws1, 'Tổng quan');
		var ws2 = XLSX.utils.aoa_to_sheet([dh].concat(dr));
		XLSX.utils.book_append_sheet(wb, ws2, 'Chi tiết');
		XLSX.writeFile(wb, name + '.xlsx');
	};

	/** Danh sách sản phẩm (Product.razor): mảng object → CSV UTF-8 BOM */
	window.exportToCsv = function (filename, rows) {
		if (!rows || !rows.length) return;
		var keys = Object.keys(rows[0]);
		function esc(v) {
			return String(v ?? '').replace(/"/g, '""');
		}
		var lines = [keys.join(',')];
		for (var i = 0; i < rows.length; i++) {
			var row = rows[i];
			lines.push(keys.map(function (k) { return '"' + esc(row[k]) + '"'; }).join(','));
		}
		var blob = new Blob(['\uFEFF' + lines.join('\n')], { type: 'text/csv;charset=utf-8' });
		var a = document.createElement('a');
		a.href = URL.createObjectURL(blob);
		a.download = (filename || 'export.csv').toString().replace(/[\\/:*?"<>|]/g, '_');
		a.click();
		URL.revokeObjectURL(a.href);
	};
})();
