import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In development the React app runs on :5173 and proxies API calls to the .NET API on :5080,
// so the browser only ever talks to one origin.
const api = process.env.VITE_API_PROXY ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': api,
      '/uploads': api,
    },
  },
})
