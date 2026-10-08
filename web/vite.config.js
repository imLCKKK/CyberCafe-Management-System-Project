import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
    plugins: [react()],
    server: {
        watch: {
            // Bỏ qua cache Visual Studio để không bị lỗi EBUSY
            ignored: ['**/.vs/**'],
        },
    },
})