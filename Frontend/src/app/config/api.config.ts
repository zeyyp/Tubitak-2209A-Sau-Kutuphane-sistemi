/**
 * API Configuration
 */
export const API_CONFIG = {
  // Yerel geliştirme
  GATEWAY_URL: 'http://localhost:5010',

  // Harici erişim için (ngrok/localtunnel kullanılırsa)
  NGROK_BACKEND_URL: null as string | null,

  // Aktif URL'i döner
  get BASE_URL(): string {
    return this.NGROK_BACKEND_URL || this.GATEWAY_URL;
  }
};
