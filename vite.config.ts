import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
    plugins: [react()],
    test: {
        environment: 'jsdom',
        globals: true,
        setupFiles: ['./src/web/test-setup.ts'],
        passWithNoTests: true,
        css: false,
    },
    // Dev server with HMR — proxies SignalR to the .NET backend
    server: {
        port: 5173,
        proxy: {
            '/hub': {
                target: 'http://localhost:5010',
                secure: false,
                ws: true,  // WebSocket support for SignalR
            },
            '/api': {
                target: 'http://localhost:5010',
                secure: false,
            },
        },
    },
    // Production build outputs to wwwroot/ for .NET to serve
    build: {
        outDir: 'wwwroot',
        emptyOutDir: false,
        sourcemap: true,
    },
});
