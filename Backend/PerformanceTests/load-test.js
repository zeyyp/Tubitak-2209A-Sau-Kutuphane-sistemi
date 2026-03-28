/**
 * k6 Load Testing Script
 * TÜBİTAK 2209-A - SAÜ Kütüphane Rezervasyon Sistemi
 * 
 * Kurulum: 
 *   Windows: winget install k6 --source winget
 *   Mac: brew install k6
 *   Linux: sudo apt install k6
 * 
 * Çalıştırma:
 *   k6 run load-test.js
 *   k6 run --vus 50 --duration 30s load-test.js
 */

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const loginDuration = new Trend('login_duration');
const reservationDuration = new Trend('reservation_duration');

// Test configuration
export const options = {
    // Aşamalı yük artışı
    stages: [
        { duration: '30s', target: 20 },   // 30 saniyede 20 kullanıcıya çık
        { duration: '1m', target: 50 },    // 1 dakika 50 kullanıcı
        { duration: '30s', target: 100 },  // 30 saniyede 100 kullanıcıya çık
        { duration: '1m', target: 100 },   // 1 dakika 100 kullanıcı tut
        { duration: '30s', target: 0 },    // 30 saniyede 0'a düş
    ],
    
    // Başarı kriterleri (TÜBİTAK: %90 başarı)
    thresholds: {
        'http_req_duration': ['p(95)<2000'],  // %95 istek 2 saniyenin altında
        'http_req_failed': ['rate<0.10'],     // Hata oranı %10'dan az (%90 başarı)
        'errors': ['rate<0.10'],              // Custom error rate
    },
};

// API URLs
const BASE_URL = 'http://localhost:5010'; // API Gateway
const IDENTITY_URL = 'http://localhost:5001';
const RESERVATION_URL = 'http://localhost:5002';

// Test data
const TEST_PASSWORD = 'Test123!';

// Generate unique student number
function generateStudentNumber() {
    return `perf_${Date.now()}_${Math.floor(Math.random() * 10000)}`;
}

// Main test scenario
export default function () {
    const studentNumber = generateStudentNumber();
    let token = null;

    // 1. KAYIT TESTİ
    group('1. User Registration', function () {
        const registerPayload = JSON.stringify({
            studentNumber: studentNumber,
            fullName: 'Performance Test User',
            email: `${studentNumber}@test.com`,
            password: TEST_PASSWORD,
            academicLevel: 'Lisans'
        });

        const registerRes = http.post(`${IDENTITY_URL}/api/Auth/register`, registerPayload, {
            headers: { 'Content-Type': 'application/json' },
            tags: { name: 'Register' }
        });

        const registerSuccess = check(registerRes, {
            'register status is 200': (r) => r.status === 200,
            'register response has token': (r) => {
                try {
                    const body = JSON.parse(r.body);
                    return body.token !== undefined;
                } catch {
                    return false;
                }
            }
        });

        errorRate.add(!registerSuccess);

        if (registerRes.status === 200) {
            try {
                const body = JSON.parse(registerRes.body);
                token = body.token;
            } catch (e) {
                // Token yok, login yapmayı dene
            }
        }
    });

    sleep(0.5);

    // 2. GİRİŞ TESTİ
    group('2. User Login', function () {
        const startTime = Date.now();
        
        const loginPayload = JSON.stringify({
            studentNumber: studentNumber,
            password: TEST_PASSWORD
        });

        const loginRes = http.post(`${IDENTITY_URL}/api/Auth/login`, loginPayload, {
            headers: { 'Content-Type': 'application/json' },
            tags: { name: 'Login' }
        });

        loginDuration.add(Date.now() - startTime);

        const loginSuccess = check(loginRes, {
            'login status is 200': (r) => r.status === 200,
            'login response has token': (r) => {
                try {
                    const body = JSON.parse(r.body);
                    return body.token !== undefined;
                } catch {
                    return false;
                }
            }
        });

        errorRate.add(!loginSuccess);

        if (loginRes.status === 200) {
            try {
                const body = JSON.parse(loginRes.body);
                token = body.token;
            } catch (e) {}
        }
    });

    sleep(0.5);

    // 3. MASA SORGULAMA TESTİ
    group('3. Get Available Tables', function () {
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        const dateStr = tomorrow.toISOString().split('T')[0];

        const tablesRes = http.get(
            `${RESERVATION_URL}/api/Reservation/Tables?date=${dateStr}&start=10:00&end=12:00&floorId=1`,
            {
                headers: token ? { 'Authorization': `Bearer ${token}` } : {},
                tags: { name: 'GetTables' }
            }
        );

        const tablesSuccess = check(tablesRes, {
            'tables status is 200': (r) => r.status === 200,
        });

        errorRate.add(!tablesSuccess);
    });

    sleep(0.5);

    // 4. FAKÜLTE LİSTESİ TESTİ
    group('4. Get Faculties', function () {
        const facultiesRes = http.get(`${RESERVATION_URL}/api/Reservation/Faculties`, {
            tags: { name: 'GetFaculties' }
        });

        const facultiesSuccess = check(facultiesRes, {
            'faculties status is 200': (r) => r.status === 200,
        });

        errorRate.add(!facultiesSuccess);
    });

    sleep(0.5);

    // 5. REZERVASYON OLUŞTURMA TESTİ
    group('5. Create Reservation', function () {
        const startTime = Date.now();
        
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        const dateStr = tomorrow.toISOString().split('T')[0];

        const reservationPayload = JSON.stringify({
            studentNumber: studentNumber,
            tableId: Math.floor(Math.random() * 10) + 1,
            reservationDate: dateStr,
            startTime: '10:00',
            endTime: '12:00'
        });

        const reservationRes = http.post(`${RESERVATION_URL}/api/Reservation/Create`, reservationPayload, {
            headers: { 
                'Content-Type': 'application/json',
                ...(token ? { 'Authorization': `Bearer ${token}` } : {})
            },
            tags: { name: 'CreateReservation' }
        });

        reservationDuration.add(Date.now() - startTime);

        const reservationSuccess = check(reservationRes, {
            'reservation status is 200 or 400': (r) => r.status === 200 || r.status === 400,
            // 400 olabilir çünkü masa dolu olabilir
        });

        errorRate.add(reservationRes.status >= 500);
    });

    sleep(1);
}

// Test summary
export function handleSummary(data) {
    const passed = data.metrics.http_req_failed.values.rate < 0.10;
    const avgDuration = data.metrics.http_req_duration.values.avg;
    const p95Duration = data.metrics.http_req_duration.values['p(95)'];
    
    console.log('\n========================================');
    console.log('  PERFORMANS TEST SONUÇLARI');
    console.log('========================================');
    console.log(`  Toplam İstek: ${data.metrics.http_reqs.values.count}`);
    console.log(`  Başarısız İstek Oranı: ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%`);
    console.log(`  Başarı Oranı: ${((1 - data.metrics.http_req_failed.values.rate) * 100).toFixed(2)}%`);
    console.log(`  Ortalama Yanıt Süresi: ${avgDuration.toFixed(0)}ms`);
    console.log(`  P95 Yanıt Süresi: ${p95Duration.toFixed(0)}ms`);
    console.log('----------------------------------------');
    console.log(`  TÜBİTAK KRİTERİ (%90): ${passed ? '✅ BAŞARILI' : '❌ BAŞARISIZ'}`);
    console.log('========================================\n');

    return {
        'stdout': textSummary(data, { indent: ' ', enableColors: true }),
        'summary.json': JSON.stringify(data, null, 2),
    };
}

import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.1/index.js';
