import qrcode from 'qrcode-generator';

import { downloadDataUrl, qrPngDataUrl, renderQrToCanvas } from './qr-code.util';

describe('qr-code.util', () => {
  const text = 'https://www.uuu.com.tw/Course/Show/1/AZ-104';

  describe('renderQrToCanvas', () => {
    const cellSize = 4;
    const margin = 4;

    /** The matrix the drawing is checked against, built independently of the util. */
    function expectedMatrix(value: string) {
      const qr = qrcode(0, 'M');
      qr.addData(value);
      qr.make();
      return qr;
    }

    /** Samples the centre of the module at (row, col); the quiet zone uses negative indexes. */
    function isDarkAt(pixels: Uint8ClampedArray, width: number, row: number, col: number): boolean {
      const x = (col + margin) * cellSize + Math.floor(cellSize / 2);
      const y = (row + margin) * cellSize + Math.floor(cellSize / 2);
      return pixels[(y * width + x) * 4] < 128;
    }

    it('sizes the canvas to the module count plus the quiet zone on both sides', () => {
      const moduleCount = expectedMatrix(text).getModuleCount();

      const canvas = renderQrToCanvas(text, { cellSize, margin });

      const expected = (moduleCount + margin * 2) * cellSize;
      expect(canvas.width).toBe(expected);
      expect(canvas.height).toBe(expected);
    });

    it('draws every module of the encoded text', () => {
      const qr = expectedMatrix(text);
      const moduleCount = qr.getModuleCount();

      const canvas = renderQrToCanvas(text, { cellSize, margin });
      const context = canvas.getContext('2d')!;
      const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;

      const mismatches: string[] = [];
      for (let row = 0; row < moduleCount; row += 1) {
        for (let col = 0; col < moduleCount; col += 1) {
          if (isDarkAt(pixels, canvas.width, row, col) !== qr.isDark(row, col)) {
            mismatches.push(`${row},${col}`);
          }
        }
      }

      expect(mismatches).toEqual([]);
    });

    it('leaves the quiet zone light', () => {
      const canvas = renderQrToCanvas(text, { cellSize, margin });
      const context = canvas.getContext('2d')!;
      const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;

      expect(isDarkAt(pixels, canvas.width, -1, -1)).toBeFalse();
      expect(isDarkAt(pixels, canvas.width, -margin, -margin)).toBeFalse();
    });
  });

  describe('qrPngDataUrl', () => {
    it('returns a PNG data URL', () => {
      const dataUrl = qrPngDataUrl(text);

      expect(dataUrl.startsWith('data:image/png;base64,')).toBeTrue();

      // The decoded payload really is a PNG, not an empty or mislabelled blob.
      const bytes = atob(dataUrl.split(',')[1]);
      expect(bytes.slice(0, 8)).toBe('\x89PNG\r\n\x1a\n');
      expect(bytes.length).toBeGreaterThan(100);
    });

    it('encodes different text to different images', () => {
      expect(qrPngDataUrl(text)).not.toBe(
        qrPngDataUrl('https://www.uuu.com.tw/Course/Show/2/AZ-104'),
      );
    });
  });

  describe('downloadDataUrl', () => {
    it('clicks an anchor carrying the data URL and the file name', () => {
      const anchor = document.createElement('a');
      const click = spyOn(anchor, 'click');
      const create = document.createElement.bind(document);
      spyOn(document, 'createElement').and.callFake((tag: string) =>
        tag === 'a' ? anchor : create(tag),
      );

      downloadDataUrl('data:image/png;base64,AAAA', 'course-AZ-104-qrcode.png');

      expect(anchor.getAttribute('href')).toBe('data:image/png;base64,AAAA');
      expect(anchor.download).toBe('course-AZ-104-qrcode.png');
      expect(click).toHaveBeenCalled();
    });
  });
});
