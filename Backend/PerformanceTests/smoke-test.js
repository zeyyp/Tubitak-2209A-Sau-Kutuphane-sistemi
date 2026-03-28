/**
 * k6 Quick Smoke Test
 * Hızlı kontrol için basit test
 * 
 * Çalıştırma: k6 run smoke-test.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    vus: 10,           // 10 sanal kullanıcı
    duration: '30s',   // 30 saniye
    thresholds: {
        'http_req_duration': ['p(95)<2000'],
        'http_req_failed': ['rate<0.10'],
    },
};

const RESERVATION_URL = 'http://localhost:5002';

export default function () {
    // Basit endpoint testi
    const res = http.get(`${RESERVATION_URL}/api/Reservation/Faculties`);
    
    check(res, {
        'status is 200': (r) => r.status === 200,
        'response time < 500ms': (r) => r.timings.duration < 500,
    });
    
    sleep(0.5);
}
