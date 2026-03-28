-- 64 Masa için SQL Script
-- Önce mevcut masaları sil
DELETE FROM "Reservations";
DELETE FROM "Tables";

-- 64 masa ekle (her kat için)
-- KAT 1 - 64 masa
INSERT INTO "Tables" ("TableNumber", "FloorId") VALUES
-- Blok 1 (Sıra 1-2: M-1 ile M-9 karşılıklı)
('Masa 1-1', 1), ('Masa 1-2', 1), ('Masa 1-3', 1), ('Masa 1-4', 1),
('Masa 1-5', 1), ('Masa 1-6', 1), ('Masa 1-7', 1), ('Masa 1-8', 1),
('Masa 1-9', 1), ('Masa 1-10', 1), ('Masa 1-11', 1), ('Masa 1-12', 1),
('Masa 1-13', 1), ('Masa 1-14', 1), ('Masa 1-15', 1), ('Masa 1-16', 1),

-- Blok 2 (Sıra 3-4)
('Masa 1-17', 1), ('Masa 1-18', 1), ('Masa 1-19', 1), ('Masa 1-20', 1),
('Masa 1-21', 1), ('Masa 1-22', 1), ('Masa 1-23', 1), ('Masa 1-24', 1),
('Masa 1-25', 1), ('Masa 1-26', 1), ('Masa 1-27', 1), ('Masa 1-28', 1),
('Masa 1-29', 1), ('Masa 1-30', 1), ('Masa 1-31', 1), ('Masa 1-32', 1),

-- Blok 3 (Sıra 5-6)
('Masa 1-33', 1), ('Masa 1-34', 1), ('Masa 1-35', 1), ('Masa 1-36', 1),
('Masa 1-37', 1), ('Masa 1-38', 1), ('Masa 1-39', 1), ('Masa 1-40', 1),
('Masa 1-41', 1), ('Masa 1-42', 1), ('Masa 1-43', 1), ('Masa 1-44', 1),
('Masa 1-45', 1), ('Masa 1-46', 1), ('Masa 1-47', 1), ('Masa 1-48', 1),

-- Blok 4 (Sıra 7-8)
('Masa 1-49', 1), ('Masa 1-50', 1), ('Masa 1-51', 1), ('Masa 1-52', 1),
('Masa 1-53', 1), ('Masa 1-54', 1), ('Masa 1-55', 1), ('Masa 1-56', 1),
('Masa 1-57', 1), ('Masa 1-58', 1), ('Masa 1-59', 1), ('Masa 1-60', 1),
('Masa 1-61', 1), ('Masa 1-62', 1), ('Masa 1-63', 1), ('Masa 1-64', 1);

-- KAT 2 - 64 masa
INSERT INTO "Tables" ("TableNumber", "FloorId") VALUES
('Masa 2-1', 2), ('Masa 2-2', 2), ('Masa 2-3', 2), ('Masa 2-4', 2),
('Masa 2-5', 2), ('Masa 2-6', 2), ('Masa 2-7', 2), ('Masa 2-8', 2),
('Masa 2-9', 2), ('Masa 2-10', 2), ('Masa 2-11', 2), ('Masa 2-12', 2),
('Masa 2-13', 2), ('Masa 2-14', 2), ('Masa 2-15', 2), ('Masa 2-16', 2),
('Masa 2-17', 2), ('Masa 2-18', 2), ('Masa 2-19', 2), ('Masa 2-20', 2),
('Masa 2-21', 2), ('Masa 2-22', 2), ('Masa 2-23', 2), ('Masa 2-24', 2),
('Masa 2-25', 2), ('Masa 2-26', 2), ('Masa 2-27', 2), ('Masa 2-28', 2),
('Masa 2-29', 2), ('Masa 2-30', 2), ('Masa 2-31', 2), ('Masa 2-32', 2),
('Masa 2-33', 2), ('Masa 2-34', 2), ('Masa 2-35', 2), ('Masa 2-36', 2),
('Masa 2-37', 2), ('Masa 2-38', 2), ('Masa 2-39', 2), ('Masa 2-40', 2),
('Masa 2-41', 2), ('Masa 2-42', 2), ('Masa 2-43', 2), ('Masa 2-44', 2),
('Masa 2-45', 2), ('Masa 2-46', 2), ('Masa 2-47', 2), ('Masa 2-48', 2),
('Masa 2-49', 2), ('Masa 2-50', 2), ('Masa 2-51', 2), ('Masa 2-52', 2),
('Masa 2-53', 2), ('Masa 2-54', 2), ('Masa 2-55', 2), ('Masa 2-56', 2),
('Masa 2-57', 2), ('Masa 2-58', 2), ('Masa 2-59', 2), ('Masa 2-60', 2),
('Masa 2-61', 2), ('Masa 2-62', 2), ('Masa 2-63', 2), ('Masa 2-64', 2);

-- KAT 3 - 64 masa
INSERT INTO "Tables" ("TableNumber", "FloorId") VALUES
('Masa 3-1', 3), ('Masa 3-2', 3), ('Masa 3-3', 3), ('Masa 3-4', 3),
('Masa 3-5', 3), ('Masa 3-6', 3), ('Masa 3-7', 3), ('Masa 3-8', 3),
('Masa 3-9', 3), ('Masa 3-10', 3), ('Masa 3-11', 3), ('Masa 3-12', 3),
('Masa 3-13', 3), ('Masa 3-14', 3), ('Masa 3-15', 3), ('Masa 3-16', 3),
('Masa 3-17', 3), ('Masa 3-18', 3), ('Masa 3-19', 3), ('Masa 3-20', 3),
('Masa 3-21', 3), ('Masa 3-22', 3), ('Masa 3-23', 3), ('Masa 3-24', 3),
('Masa 3-25', 3), ('Masa 3-26', 3), ('Masa 3-27', 3), ('Masa 3-28', 3),
('Masa 3-29', 3), ('Masa 3-30', 3), ('Masa 3-31', 3), ('Masa 3-32', 3),
('Masa 3-33', 3), ('Masa 3-34', 3), ('Masa 3-35', 3), ('Masa 3-36', 3),
('Masa 3-37', 3), ('Masa 3-38', 3), ('Masa 3-39', 3), ('Masa 3-40', 3),
('Masa 3-41', 3), ('Masa 3-42', 3), ('Masa 3-43', 3), ('Masa 3-44', 3),
('Masa 3-45', 3), ('Masa 3-46', 3), ('Masa 3-47', 3), ('Masa 3-48', 3),
('Masa 3-49', 3), ('Masa 3-50', 3), ('Masa 3-51', 3), ('Masa 3-52', 3),
('Masa 3-53', 3), ('Masa 3-54', 3), ('Masa 3-55', 3), ('Masa 3-56', 3),
('Masa 3-57', 3), ('Masa 3-58', 3), ('Masa 3-59', 3), ('Masa 3-60', 3),
('Masa 3-61', 3), ('Masa 3-62', 3), ('Masa 3-63', 3), ('Masa 3-64', 3);

-- Toplam: 192 masa (3 kat x 64 masa)
SELECT 'Toplam masa sayısı: ' || COUNT(*) FROM "Tables";
