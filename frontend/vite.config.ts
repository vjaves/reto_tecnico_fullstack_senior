import fs from 'node:fs'
import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// HTTPS de punta a punta en desarrollo:
//   navegador ──HTTPS──▶ Vite (5173) ──HTTPS──▶ API (7080)
// Vite reutiliza el certificado de desarrollo de ASP.NET (exportado con `npm run certs`), que ya es de
// confianza en el equipo. Si no se exportó, Vite sirve por HTTP y lo avisa en consola.
const certDir = path.resolve(process.cwd(), 'certs')
const cert = path.join(certDir, 'localhost.pem')
const key = path.join(certDir, 'localhost.key')
const https = fs.existsSync(cert) && fs.existsSync(key) ? { cert: fs.readFileSync(cert), key: fs.readFileSync(key) } : undefined

if (!https) console.warn('\n[vite] Sin certificado en ./certs: se sirve por HTTP. Ejecute `npm run certs` para HTTPS.\n')

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')
  const apiUrl = env.VITE_API_PROXY || 'https://localhost:7080'

  // Node no usa el almacén de certificados de Windows: para el salto Vite → API (localhost) no se verifica
  // la cadena. El navegador sí la verifica en el salto que importa (navegador → Vite).
  const proxy = { target: apiUrl, secure: false, changeOrigin: false }

  return {
    plugins: [react(), tailwindcss()],
    server: {
      port: 5173,
      strictPort: true,
      https,
      proxy: {
        '/api': proxy,
        '/auth': proxy,
      },
    },
  }
})
