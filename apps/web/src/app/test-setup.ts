// Node 22+ exposes a built-in `localStorage` global that vitest's jsdom env
// surfaces on `window` instead of jsdom's own Storage. Без `--localstorage-file`
// у этого встроенного объекта нет методов (`getItem is not a function`), и
// компоненты, которые читают ширину панели из localStorage, падают на старте
// теста. Подменяем window.localStorage / window.sessionStorage свежим in-memory
// Storage перед каждым тестом — тесты в jsdom не должны течь между файлами.
import { beforeEach } from "vitest";

function createMemoryStorage(): Storage {
  let store = new Map<string, string>();
  return {
    get length() {
      return store.size;
    },
    clear() {
      store = new Map();
    },
    getItem(key: string) {
      return store.has(key) ? (store.get(key) ?? null) : null;
    },
    key(index: number) {
      return Array.from(store.keys())[index] ?? null;
    },
    removeItem(key: string) {
      store.delete(key);
    },
    setItem(key: string, value: string) {
      store.set(key, value);
    }
  } satisfies Storage;
}

function installStorage(): void {
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    value: createMemoryStorage()
  });
  Object.defineProperty(window, "sessionStorage", {
    configurable: true,
    value: createMemoryStorage()
  });
}

installStorage();

beforeEach(() => {
  installStorage();
});
