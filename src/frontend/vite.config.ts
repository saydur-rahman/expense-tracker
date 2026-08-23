import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // Aspire allocates the port and passes it in PORT; its proxy forwards to
    // exactly that port, so Vite must bind there rather than picking its own.
    // strictPort makes a clash fail loudly instead of silently drifting.
    port: Number(process.env.PORT) || 5173,
    strictPort: true,
  },
})
