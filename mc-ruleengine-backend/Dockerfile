# Usa una imagen base de Node.js
FROM node:18-alpine

# Establece el directorio de trabajo
WORKDIR /app

# Copia los archivos del proyecto
COPY package*.json tsconfig.json ./

# Instala las dependencias
RUN npm install

# Copia el resto del código
COPY ./src ./src

# Copia el archivo .env
COPY .env .env

# Compila el código TypeScript a JavaScript
RUN npm run build

# Expone el puerto 3001
EXPOSE 3001

# Comando para iniciar la aplicación
CMD ["node", "dist/app.js"]