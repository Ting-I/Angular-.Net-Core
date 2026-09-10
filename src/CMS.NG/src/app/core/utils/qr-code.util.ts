/**
 * QR code helpers.
 *
 * `qrcode-generator` only produces the module matrix and a GIF data URL; the rendering below draws
 * that matrix onto a canvas so the page and the download both work from a PNG.
 */
import qrcode from 'qrcode-generator';

export interface QrCodeOptions {
  /** Pixels per QR module. */
  cellSize?: number;
  /** Quiet-zone width in modules. Four is the minimum the QR spec asks for. */
  margin?: number;
  dark?: string;
  light?: string;
}

const DEFAULTS: Required<QrCodeOptions> = {
  cellSize: 6,
  margin: 4,
  dark: '#000000',
  light: '#ffffff',
};

/**
 * Draws `text` as a QR code onto a fresh canvas.
 *
 * Type number 0 lets the library pick the smallest symbol that fits; error correction `M` is the
 * usual choice for a printed URL.
 */
export function renderQrToCanvas(text: string, options: QrCodeOptions = {}): HTMLCanvasElement {
  const { cellSize, margin, dark, light } = { ...DEFAULTS, ...options };

  const qr = qrcode(0, 'M');
  qr.addData(text);
  qr.make();

  const moduleCount = qr.getModuleCount();
  const size = (moduleCount + margin * 2) * cellSize;

  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;

  const context = canvas.getContext('2d');
  if (!context) {
    return canvas;
  }

  // The quiet zone is part of the symbol — a QR drawn edge to edge does not scan reliably.
  context.fillStyle = light;
  context.fillRect(0, 0, size, size);

  context.fillStyle = dark;
  for (let row = 0; row < moduleCount; row += 1) {
    for (let col = 0; col < moduleCount; col += 1) {
      if (qr.isDark(row, col)) {
        context.fillRect((col + margin) * cellSize, (row + margin) * cellSize, cellSize, cellSize);
      }
    }
  }

  return canvas;
}

/** `text` as a `image/png` data URL, ready for an `<img src>` or an `<a download>`. */
export function qrPngDataUrl(text: string, options: QrCodeOptions = {}): string {
  return renderQrToCanvas(text, options).toDataURL('image/png');
}

/** Hands `dataUrl` to the browser as a file download named `filename`. */
export function downloadDataUrl(dataUrl: string, filename: string): void {
  const link = document.createElement('a');
  link.href = dataUrl;
  link.download = filename;
  link.click();
}
