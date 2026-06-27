import { cleanup, render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { TerminalView } from "./TerminalView";

// xterm трогает DOM/Canvas, которых нет в jsdom — подменяем тонкими заглушками.
const terminalWrites: string[] = [];
vi.mock("@xterm/xterm/css/xterm.css", () => ({}));
vi.mock("@xterm/xterm", () => ({
  Terminal: class {
    cols = 80;
    rows = 24;
    loadAddon() {
      /* noop */
    }
    open() {
      /* noop */
    }
    onData() {
      return { dispose: vi.fn() };
    }
    write(data: string) {
      terminalWrites.push(data);
    }
    dispose() {
      /* noop */
    }
  }
}));
vi.mock("@xterm/addon-fit", () => ({
  FitAddon: class {
    fit() {
      /* noop */
    }
  }
}));

class FakeWebSocket {
  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSING = 2;
  static CLOSED = 3;
  static instances: FakeWebSocket[] = [];

  readyState = FakeWebSocket.OPEN;
  binaryType = "blob";
  readonly url: string;
  readonly sent: string[] = [];
  private readonly listeners = new Map<string, Set<(e: unknown) => void>>();

  constructor(url: string) {
    this.url = url;
    FakeWebSocket.instances.push(this);
  }
  addEventListener(type: string, cb: (e: unknown) => void) {
    const set = this.listeners.get(type) ?? new Set();
    set.add(cb);
    this.listeners.set(type, set);
  }
  send(data: string) {
    this.sent.push(data);
  }
  close() {
    this.readyState = FakeWebSocket.CLOSED;
  }
  emit(type: string, event: unknown) {
    this.listeners.get(type)?.forEach((cb) => {
      cb(event);
    });
  }
}

function lastSocket(): FakeWebSocket {
  const socket = FakeWebSocket.instances.at(-1);
  if (socket === undefined) throw new Error("WebSocket не был создан");
  return socket;
}

describe("TerminalView", () => {
  let rafCallbacks: FrameRequestCallback[] = [];

  beforeEach(() => {
    FakeWebSocket.instances = [];
    terminalWrites.length = 0;
    rafCallbacks = [];
    vi.stubGlobal("WebSocket", FakeWebSocket);
    vi.stubGlobal("requestAnimationFrame", (cb: FrameRequestCallback) => {
      rafCallbacks.push(cb);
      return rafCallbacks.length;
    });
    vi.stubGlobal("cancelAnimationFrame", vi.fn());
    vi.stubGlobal(
      "ResizeObserver",
      class {
        observe() {
          /* noop */
        }
        disconnect() {
          /* noop */
        }
      }
    );
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("не сигналит onClosed при плановом размонтировании (tmux-сессия жива)", () => {
    const onClosed = vi.fn();
    const { unmount } = render(
      <TerminalView intentId="i1" attempt={1} onClosed={onClosed} />
    );

    const socket = lastSocket();
    unmount();
    // Cleanup сам закрыл сокет — его отложенный close не должен трактоваться
    // как конец сессии.
    socket.emit("close", { code: 1000 });

    expect(onClosed).not.toHaveBeenCalled();
  });

  it("сигналит onClosed при реальном закрытии сокета во время жизни вью", () => {
    const onClosed = vi.fn();
    render(<TerminalView intentId="i1" attempt={1} onClosed={onClosed} />);

    lastSocket().emit("close", { code: 1006 });

    expect(onClosed).toHaveBeenCalledWith(1006);
  });

  it("первый resize шлёт только после загрузки шрифта и форсит redraw (C-l)", async () => {
    render(<TerminalView intentId="i1" attempt={1} onClosed={vi.fn()} />);

    const socket = lastSocket();
    // Сокет открылся раньше, чем догрузился шрифт — геометрию ещё не шлём.
    socket.emit("open", {});
    expect(socket.sent).toEqual([]);

    // Даём промису шрифта (document.fonts недоступен в jsdom → resolved-цепочка)
    // отработать на микротасках.
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();

    const frames = socket.sent.map(
      (raw) => JSON.parse(raw) as Record<string, unknown>
    );
    expect(frames).toContainEqual({ type: "resize", cols: 80, rows: 24 });
    expect(frames).toContainEqual({ type: "input", data: "\f" });
  });

  it("склеивает несколько output frames в один term.write на animation frame", () => {
    render(<TerminalView intentId="i1" attempt={1} onClosed={vi.fn()} />);

    const socket = lastSocket();
    socket.emit("message", { data: '{"type":"output","data":"a"}' });
    socket.emit("message", { data: '{"type":"output","data":"b"}' });

    expect(terminalWrites).toEqual([]);
    rafCallbacks.shift()?.(16);

    expect(terminalWrites).toEqual(["ab"]);
  });
});
