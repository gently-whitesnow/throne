import path from "node:path";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vitest/config";

// В dev SPA крутится на корне (`vite dev` через прокси к локальному throne-api).
// В prod-сборке за Caddy SPA смонтирована под /app/ — отдельная зона от auth-web (/login)
// и будущего лендинга (/). Префикс зашит в Vite (asset URL'ы) и в react-router (basename
// читается из `import.meta.env.BASE_URL`).
export default defineConfig(({ command }) => ({
  base: command === "build" ? "/app/" : "/",
  plugins: [tailwindcss(), react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src")
    }
  },
  server: {
    proxy: {
      "/api": {
        target: process.env.VITE_API_PROXY_TARGET ?? "http://127.0.0.1:5008",
        changeOrigin: true
      }
    }
  },
  test: {
    environment: "jsdom"
  }
}));
