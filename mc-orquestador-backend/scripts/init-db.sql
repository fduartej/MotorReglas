-- Create databases
CREATE DATABASE IF NOT EXISTS audit;

-- Create sample tables for testing
CREATE TABLE IF NOT EXISTS clientes (
    id SERIAL PRIMARY KEY,
    dni VARCHAR(20) UNIQUE NOT NULL,
    nombre VARCHAR(100) NOT NULL,
    apellido VARCHAR(100) NOT NULL,
    email VARCHAR(255),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS direcciones (
    id SERIAL PRIMARY KEY,
    cliente_id INTEGER REFERENCES clientes(id),
    tipo VARCHAR(20) DEFAULT 'principal',
    direccion VARCHAR(255),
    ciudad_id INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS ciudades (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    provincia_id INTEGER
);

CREATE TABLE IF NOT EXISTS provincias (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS categorias_producto (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL
);

CREATE TABLE IF NOT EXISTS productos (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    categoria_id INTEGER REFERENCES categorias_producto(id)
);

CREATE TABLE IF NOT EXISTS sucursales (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(10) UNIQUE NOT NULL,
    nombre VARCHAR(100) NOT NULL
);

CREATE TABLE IF NOT EXISTS cuentas (
    id SERIAL PRIMARY KEY,
    dni VARCHAR(20) NOT NULL,
    numero_cuenta VARCHAR(50) UNIQUE NOT NULL,
    tipo_cuenta VARCHAR(50) NOT NULL,
    saldo_actual DECIMAL(15,2) DEFAULT 0,
    estado VARCHAR(20) DEFAULT 'activa',
    producto_id INTEGER REFERENCES productos(id),
    sucursal_id INTEGER REFERENCES sucursales(id),
    fecha_apertura TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS deudas (
    id SERIAL PRIMARY KEY,
    dni VARCHAR(20) NOT NULL,
    monto DECIMAL(15,2) NOT NULL,
    estado VARCHAR(10) DEFAULT 'PEND',
    fecha TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Insert sample data
INSERT INTO categorias_producto (nombre) VALUES 
    ('BASICO'), ('PREMIUM'), ('EMPRESARIAL');

INSERT INTO productos (nombre, categoria_id) VALUES 
    ('Cuenta Corriente', 1),
    ('Cuenta Premium', 2),
    ('Cuenta Empresarial', 3);

INSERT INTO sucursales (codigo, nombre) VALUES 
    ('001', 'Sucursal Centro'),
    ('002', 'Sucursal Norte'),
    ('003', 'Sucursal Sur');

INSERT INTO provincias (nombre) VALUES 
    ('Lima'), ('Arequipa'), ('Cusco');

INSERT INTO ciudades (nombre, provincia_id) VALUES 
    ('Lima', 1), ('Arequipa', 2), ('Cusco', 3);

INSERT INTO clientes (dni, nombre, apellido, email) VALUES 
    ('12345678', 'Juan', 'Perez', 'juan.perez@email.com'),
    ('87654321', 'Maria', 'Garcia', 'maria.garcia@email.com'),
    ('11111111', 'Carlos', 'Rodriguez', 'carlos.rodriguez@email.com');

INSERT INTO direcciones (cliente_id, tipo, direccion, ciudad_id) VALUES 
    (1, 'principal', 'Av. Principal 123', 1),
    (2, 'principal', 'Calle Secundaria 456', 2),
    (3, 'principal', 'Jr. Terciario 789', 3);

INSERT INTO cuentas (dni, numero_cuenta, tipo_cuenta, saldo_actual, estado, producto_id, sucursal_id) VALUES 
    ('12345678', '001-123456', 'CORRIENTE', 15000.00, 'activa', 2, 1),
    ('12345678', '001-789012', 'AHORROS', 5000.00, 'activa', 1, 1),
    ('87654321', '002-345678', 'CORRIENTE', 25000.00, 'preferente', 2, 2),
    ('11111111', '003-567890', 'EMPRESARIAL', 100000.00, 'activa', 3, 3);

INSERT INTO deudas (dni, monto, estado, fecha) VALUES 
    ('12345678', 1500.00, 'PEND', CURRENT_TIMESTAMP - INTERVAL '30 days'),
    ('12345678', 800.00, 'VENC', CURRENT_TIMESTAMP - INTERVAL '60 days'),
    ('87654321', 2200.00, 'PEND', CURRENT_TIMESTAMP - INTERVAL '15 days');

-- Create indexes for performance
CREATE INDEX idx_clientes_dni ON clientes(dni);
CREATE INDEX idx_cuentas_dni ON cuentas(dni);
CREATE INDEX idx_deudas_dni ON deudas(dni);
CREATE INDEX idx_direcciones_cliente ON direcciones(cliente_id);
