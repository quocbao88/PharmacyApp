-- ==========================================
-- PostgreSQL/Supabase Complete DDL & DML Script
-- ==========================================

-- 1. DROP TABLES IF THEY ALREADY EXIST (Optional, comment out if you want to keep existing data)
-- DROP TABLE IF EXISTS order_details;
-- DROP TABLE IF EXISTS orders;
-- DROP TABLE IF EXISTS shifts;
-- DROP TABLE IF EXISTS product_unit_conversions;
-- DROP TABLE IF EXISTS product_batches;
-- DROP TABLE IF EXISTS products;
-- DROP TABLE IF EXISTS product_audit_logs;
-- DROP TABLE IF EXISTS suppliers;
-- DROP TABLE IF EXISTS users;
-- DROP TABLE IF EXISTS customers;

-- 2. CREATE TABLES (DDL)

CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    password_hash VARCHAR(500) NOT NULL,
    full_name VARCHAR(200) NOT NULL,
    role VARCHAR(50) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS suppliers (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    contact_person VARCHAR(200),
    phone VARCHAR(50),
    address VARCHAR(500),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS products (
    id UUID PRIMARY KEY,
    supplier_id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    active_ingredient VARCHAR(300),
    category VARCHAR(150),
    manufacturer VARCHAR(200),
    dosage_form VARCHAR(100),
    strength VARCHAR(100),
    storage_conditions VARCHAR(500),
    prescription_required BOOLEAN NOT NULL DEFAULT FALSE,
    description TEXT,
    unit VARCHAR(50) NOT NULL,
    import_price NUMERIC(18, 2) NOT NULL,
    selling_price NUMERIC(18, 2) NOT NULL,
    min_stock_level INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_products_suppliers_supplier_id FOREIGN KEY (supplier_id) REFERENCES suppliers(id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_products_name ON products(name);
CREATE INDEX IF NOT EXISTS ix_products_supplier_id ON products(supplier_id);

CREATE TABLE IF NOT EXISTS product_batches (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL,
    batch_number VARCHAR(100) NOT NULL,
    expiration_date TIMESTAMP WITH TIME ZONE NOT NULL,
    current_quantity INTEGER NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT "CK_ProductBatch_Quantity" CHECK (current_quantity >= 0),
    CONSTRAINT fk_product_batches_products_product_id FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_product_batches_expiration_date ON product_batches(expiration_date);
CREATE INDEX IF NOT EXISTS ix_product_batches_product_id_expiration_date ON product_batches(product_id, expiration_date);

CREATE TABLE IF NOT EXISTS customers (
    id UUID PRIMARY KEY,
    full_name VARCHAR(200) NOT NULL,
    phone VARCHAR(50) NOT NULL UNIQUE,
    allergy_notes TEXT,
    reward_points INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_customers_phone ON customers(phone);

CREATE TABLE IF NOT EXISTS shifts (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    start_time TIMESTAMP WITH TIME ZONE NOT NULL,
    end_time TIMESTAMP WITH TIME ZONE,
    starting_cash NUMERIC NOT NULL,
    ending_cash NUMERIC,
    actual_revenue NUMERIC NOT NULL DEFAULT 0,
    status TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_shifts_users_user_id FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_shifts_user_id ON shifts(user_id);

CREATE TABLE IF NOT EXISTS orders (
    id UUID PRIMARY KEY,
    order_code VARCHAR(100) NOT NULL UNIQUE,
    user_id UUID NOT NULL,
    customer_id UUID,
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    total_amount NUMERIC(18, 2) NOT NULL,
    discount_amount NUMERIC(18, 2) NOT NULL,
    payment_method VARCHAR(50) NOT NULL,
    national_sync_status TEXT,
    national_sync_message TEXT,
    national_synced_at TIMESTAMP WITH TIME ZONE,
    prescription_code TEXT,
    prescribing_doctor TEXT,
    medical_facility TEXT,
    diagnostic TEXT,
    CONSTRAINT fk_orders_customers_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE SET NULL,
    CONSTRAINT fk_orders_users_user_id FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_orders_customer_id ON orders(customer_id);
CREATE INDEX IF NOT EXISTS ix_orders_order_code ON orders(order_code);
CREATE INDEX IF NOT EXISTS ix_orders_user_id ON orders(user_id);

CREATE TABLE IF NOT EXISTS order_details (
    id UUID PRIMARY KEY,
    order_id UUID NOT NULL,
    product_id UUID NOT NULL,
    batch_id UUID NOT NULL,
    quantity INTEGER NOT NULL,
    unit_price NUMERIC(18, 2) NOT NULL,
    cost_price NUMERIC(18, 2) NOT NULL,
    subtotal NUMERIC(18, 2) NOT NULL,
    sold_unit TEXT,
    conversion_value INTEGER NOT NULL,
    CONSTRAINT "CK_OrderDetail_Quantity" CHECK (quantity > 0),
    CONSTRAINT fk_order_details_orders_order_id FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE CASCADE,
    CONSTRAINT fk_order_details_product_batches_batch_id FOREIGN KEY (batch_id) REFERENCES product_batches(id) ON DELETE RESTRICT,
    CONSTRAINT fk_order_details_products_product_id FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_order_details_batch_id ON order_details(batch_id);
CREATE INDEX IF NOT EXISTS ix_order_details_order_id ON order_details(order_id);
CREATE INDEX IF NOT EXISTS ix_order_details_product_id ON order_details(product_id);

CREATE TABLE IF NOT EXISTS product_unit_conversions (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL,
    unit_name VARCHAR(50) NOT NULL,
    conversion_value INTEGER NOT NULL,
    selling_price NUMERIC(18, 2) NOT NULL,
    CONSTRAINT fk_product_unit_conversions_products_product_id FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_product_unit_conversions_product_id ON product_unit_conversions(product_id);

CREATE TABLE IF NOT EXISTS product_audit_logs (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL,
    product_name TEXT,
    action VARCHAR(100) NOT NULL,
    changed_by VARCHAR(200) NOT NULL,
    changed_at TIMESTAMP WITH TIME ZONE NOT NULL,
    details VARCHAR(2000)
);

-- 3. INSERT SEED DATA (DML)

-- Insert Default Supplier
INSERT INTO suppliers (id, name, contact_person, phone, address, created_at)
VALUES ('d3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Tổng công ty Dược phẩm Trung ương I', 'Nguyễn Văn B', '0901234567', '160 Tôn Đức Thắng, Đống Đa, Hà Nội', NOW())
ON CONFLICT (id) DO NOTHING;

-- Insert Default Users (Password hashes for admin123 and staff123)
INSERT INTO users (id, username, password_hash, full_name, role, created_at)
VALUES 
('8a071720-6d0e-4ab8-912a-434cb2f57a3e', 'admin', 'JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=', 'Quản trị hệ thống', 'Admin', NOW()),
('e8fd7d32-2d1c-43f6-8c4d-df7870a4a980', 'staff', 'EBdue3sk0xes/PjSBkz9LyThVPe1qWYDB31e+BPWprY=', 'Dược sĩ Nguyễn Văn A', 'Staff', NOW())
ON CONFLICT (username) DO NOTHING;

-- Insert Products (23 items from medicine service report)
INSERT INTO products (id, supplier_id, name, active_ingredient, category, manufacturer, dosage_form, strength, storage_conditions, prescription_required, description, unit, import_price, selling_price, min_stock_level, created_at, updated_at)
VALUES
('47a285d8-c9c0-43eb-b8f2-89bd36cb47a3', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Bơm 10ml', NULL, 'Thiết bị y tế', NULL, NULL, '10ml', NULL, FALSE, 'Bơm kim tiêm 10ml', 'Cái', 1200, 3000, 10, NOW(), NOW()),
('2f41d994-df73-455b-800a-4fb48d7c9a91', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Bơm 5ml', NULL, 'Thiết bị y tế', NULL, NULL, '5ml', NULL, FALSE, 'Bơm kim tiêm 5ml', 'Cái', 1000, 3000, 10, NOW(), NOW()),
('9d6c3748-0cf8-4d51-83d8-a83d29a50ef1', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Cefixime 200mg', 'Cefixime', 'Thuốc', NULL, 'Viên nén', '200mg', NULL, TRUE, 'Thuốc kháng sinh', 'Viên', 3200, 5000, 50, NOW(), NOW()),
('ea684d0b-6072-46a4-9275-6e012cdbfcb2', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Dao cắt chỉ', NULL, 'Thiết bị y tế', NULL, NULL, NULL, NULL, FALSE, 'Dụng cụ cắt chỉ phẫu thuật', 'Cái', 950, 10000, 5, NOW(), NOW()),
('955a12bd-7d1a-4d2c-8ab5-0453de69b828', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Dây truyền dịch', NULL, 'Thiết bị y tế', NULL, NULL, NULL, NULL, FALSE, 'Dây truyền dịch vô trùng', 'Bịch', 4000, 5000, 10, NOW(), NOW()),
('bc6bdf77-3e11-47cc-9818-b7eb4196144e', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Effe 500mg', 'Paracetamol', 'Thuốc', NULL, 'Viên sủi', '500mg', NULL, FALSE, 'Thuốc giảm đau hạ sốt sủi bọt', 'Viên', 3000, 5000, 20, NOW(), NOW()),
('dfdfb2b5-e6a3-485c-a5b6-c567954d2417', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Fegamide', NULL, 'Thuốc', NULL, 'Ống', NULL, NULL, FALSE, 'Dung dịch uống bổ sung', 'Ống', 24000, 40000, 10, NOW(), NOW()),
('f44ccae6-6df3-4c91-a1e6-df05b1876543', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Kim bướm (xanh + vàng)', NULL, 'Thiết bị y tế', NULL, NULL, NULL, NULL, FALSE, 'Kim cánh bướm lấy máu/truyền dịch', 'Cái', 1200, 5000, 20, NOW(), NOW()),
('75c02b1f-e9b4-4b53-b27b-fb8e5c1a7d60', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Kim châm cứu', NULL, 'Thiết bị y tế', NULL, NULL, NULL, NULL, FALSE, 'Kim dùng trong châm cứu trị liệu', 'Cái', 320, 1000, 100, NOW(), NOW()),
('25cde6cf-1bfb-4f9f-8647-38e2172778cd', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Lidocain ống', 'Lidocaine', 'Thuốc', NULL, 'Ống tiêm', '2%', NULL, TRUE, 'Thuốc gây tê tại chỗ', 'Hộp', 700, 10000, 10, NOW(), NOW()),
('ad9e334a-9b16-43b6-bfb2-60197d10f882', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Medrokort 40 (solu nội)', 'Methylprednisolone', 'Thuốc', NULL, 'Lọ bột pha tiêm', '40mg', NULL, TRUE, 'Thuốc kháng viêm Corticoid', 'Lọ', 28000, 60000, 10, NOW(), NOW()),
('ca67c541-e129-4e78-9e58-f9bde2f91df5', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Methylprednisolon 16mg', 'Methylprednisolone', 'Thuốc', NULL, 'Viên nén', '16mg', NULL, TRUE, 'Thuốc kháng viêm Corticoid đường uống', 'Viên', 870, 2000, 20, NOW(), NOW()),
('5f89be2a-13a8-4e1b-90f7-ebf423ab11f8', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Nacl 0.9%', 'Natri Clorid', 'Thuốc', NULL, 'Chai truyền', '0.9%', NULL, FALSE, 'Nước muối sinh lý truyền dịch', 'Chai', 12000, 30000, 10, NOW(), NOW()),
('fcd86f4a-fa13-4df4-8d4e-e17918a5cb16', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Ngải cứu', NULL, 'Thuốc', NULL, NULL, NULL, NULL, FALSE, 'Ngải cứu trị liệu đông y', 'Cái', 6500, 10000, 10, NOW(), NOW()),
('ae9f18a2-25de-4b13-a4c0-7fbe8b5a17e0', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Nước cất', 'Nước cất pha tiêm', 'Thuốc', NULL, 'Ống/Viên', NULL, NULL, FALSE, 'Nước cất dùng pha tiêm', 'Viên', 900, 2000, 50, NOW(), NOW()),
('6d3fbcd7-4eb0-4d43-85b2-32bc8fb91d2c', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Piracetam 1g', 'Piracetam', 'Thuốc', NULL, 'Ống tiêm', '1g', NULL, TRUE, 'Thuốc bổ não, tăng tuần hoàn não', 'Ống', 8000, 30000, 15, NOW(), NOW()),
('5e8acbf0-22c6-48eb-a1d2-bc324c5678a9', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Ringerlactat', 'Ringer Lactate', 'Thuốc', NULL, 'Chai truyền', '500ml', NULL, FALSE, 'Dịch truyền bù nước điện giải', 'Chai', 12000, 30000, 10, NOW(), NOW()),
('8d867c4f-c0d1-4e7b-944a-d1e9f1a234b6', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Ugo (Gạc mỡ)', NULL, 'Thuốc', NULL, 'Miếng gạc', NULL, NULL, FALSE, 'Băng cá nhân / Gạc mỡ chống dính', 'Miếng', 45000, 55000, 5, NOW(), NOW()),
('11cde23f-e145-4de6-91b4-2cdbf72c78a3', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Test cúm', NULL, 'Thiết bị y tế', NULL, 'Khay thử', NULL, NULL, FALSE, 'Bộ test nhanh chẩn đoán cúm A/B', 'Test', 38000, 50000, 5, NOW(), NOW()),
('cb8fd2d9-1b5e-4df6-8cd4-5fbe674d89a2', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Katrypsin (Alphachymo) 5mg', 'Alphachymotrypsin', 'Thuốc', NULL, 'Viên nén', '5mg', NULL, FALSE, 'Thuốc chống phù nề, kháng viêm dạng men', 'Viên', 700, 1000, 100, NOW(), NOW()),
('59fe1cd7-ef4c-45a8-ac3d-0cbdf13d52c1', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Cerebrolysin', 'Cerebrolysin peptit', 'Thuốc', NULL, 'Ống tiêm', '10ml', NULL, TRUE, 'Thuốc dinh dưỡng thần kinh', 'Ống', 115000, 120000, 5, NOW(), NOW()),
('44fde16d-318e-4a6c-9411-fbde8cb45e6f', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Tanganil', 'Acetylleucine', 'Thuốc', NULL, 'Ống tiêm', '500mg/5ml', NULL, TRUE, 'Thuốc điều trị chứng chóng mặt', 'Ống', 21000, 60000, 5, NOW(), NOW()),
('98bfcd84-bfe1-4c12-8abf-4cdbf781a95e', 'd3b07384-d113-4a1e-848e-a22c54d1e6c2', 'Kim lấy thuốc', NULL, 'Thiết bị y tế', NULL, NULL, NULL, NULL, FALSE, 'Kim lấy thuốc chuyên dụng', 'Cái', 1000, 2000, 20, NOW(), NOW())
ON CONFLICT (id) DO NOTHING;

-- Insert Initial Batches with Final Inventory Quantity (Tồn cuối)
INSERT INTO product_batches (id, product_id, batch_number, expiration_date, current_quantity, created_at)
VALUES
('b1111111-1111-4111-a111-111111111111', '47a285d8-c9c0-43eb-b8f2-89bd36cb47a3', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 47, NOW()),
('b2222222-2222-4222-a222-222222222222', '2f41d994-df73-455b-800a-4fb48d7c9a91', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 55, NOW()),
('b3333333-3333-4333-a333-333333333333', '9d6c3748-0cf8-4d51-83d8-a83d29a50ef1', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 90, NOW()),
('b4444444-4444-4444-a444-444444444444', 'ea684d0b-6072-46a4-9275-6e012cdbfcb2', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 67, NOW()),
('b5555555-5555-4555-a555-555555555555', '955a12bd-7d1a-4d2c-8ab5-0453de69b828', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 32, NOW()),
('b6666666-6666-4666-a666-666666666666', 'bc6bdf77-3e11-47cc-9818-b7eb4196144e', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 9, NOW()),
('b7777777-7777-4777-a777-777777777777', 'dfdfb2b5-e6a3-485c-a5b6-c567954d2417', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 16, NOW()),
('b8888888-8888-4888-a888-888888888888', 'f44ccae6-6df3-4c91-a1e6-df05b1876543', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 16, NOW()),
('b9999999-9999-4999-a999-999999999999', '75c02b1f-e9b4-4b53-b27b-fb8e5c1a7d60', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 2880, NOW()),
('ba101010-1010-4101-a101-101010101010', '25cde6cf-1bfb-4f9f-8647-38e2172778cd', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 64, NOW()),
('ba111111-1111-4111-a111-111111111111', 'ad9e334a-9b16-43b6-bfb2-60197d10f882', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 36, NOW()),
('ba121212-1212-4121-a121-121212121212', 'ca67c541-e129-4e78-9e58-f9bde2f91df5', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 19, NOW()),
('ba131313-1313-4131-a131-131313131313', '5f89be2a-13a8-4e1b-90f7-ebf423ab11f8', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 28, NOW()),
('ba141414-1414-4141-a141-141414141414', 'fcd86f4a-fa13-4df4-8d4e-e17918a5cb16', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 75, NOW()),
('ba151515-1515-4151-a151-151515151515', 'ae9f18a2-25de-4b13-a4c0-7fbe8b5a17e0', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 29, NOW()),
('ba161616-1616-4161-a161-161616161616', '6d3fbcd7-4eb0-4d43-85b2-32bc8fb91d2c', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 28, NOW()),
('ba171717-1717-4171-a171-171717171717', '5e8acbf0-22c6-48eb-a1d2-bc324c5678a9', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 21, NOW()),
('ba181818-1818-4181-a181-181818181818', '8d867c4f-c0d1-4e7b-944a-d1e9f1a234b6', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 2, NOW()),
('ba191919-1919-4191-a191-191919191919', '11cde23f-e145-4de6-91b4-2cdbf72c78a3', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 0, NOW()),
('ba202020-2020-4202-a202-202020202020', 'cb8fd2d9-1b5e-4df6-8cd4-5fbe674d89a2', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 290, NOW()),
('ba212121-2121-4212-a212-212121212121', '59fe1cd7-ef4c-45a8-ac3d-0cbdf13d52c1', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 10, NOW()),
('ba222222-2222-4222-a222-222222222222', '44fde16d-318e-4a6c-9411-fbde8cb45e6f', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 5, NOW()),
('ba232323-2323-4232-a232-232323232323', '98bfcd84-bfe1-4c12-8abf-4cdbf781a95e', 'LOT-INITIAL', NOW() + INTERVAL '2 years', 2, NOW())
ON CONFLICT (id) DO NOTHING;

-- 5. Register Migration in EF Core Migration History Table
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260702080317_InitialSupabasePostgres', '6.0.36')
ON CONFLICT ("MigrationId") DO NOTHING;

