# Build stage
FROM node:20-alpine AS builder

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm install

# Copy source code
COPY . .

# Build args - API key injected at build time
ARG VITE_API_KEY

# Set environment variable for build
ENV VITE_API_KEY=${VITE_API_KEY}

# Build the application
RUN npm run build

# Production stage
FROM nginx:alpine

# Build arg reused in nginx stage (inject API key at proxy layer)
ARG VITE_API_KEY

# Copy nginx configuration with proxy support
RUN printf '%s\n' "\
server {\
    listen 3000;\
    root /usr/share/nginx/html;\
    index index.html;\
    try_files \$uri \$uri/ /index.html;\
\
    location /api/ {\
        proxy_pass http://data-collector:8080;\
        proxy_http_version 1.1;\
        proxy_set_header Upgrade \$http_upgrade;\
        proxy_set_header Connection \"upgrade\";\
        proxy_set_header Host \$host;\
        proxy_set_header X-Real-IP \$remote_addr;\
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;\
        proxy_set_header X-Forwarded-Proto \$scheme;\
        proxy_set_header X-API-Key ${VITE_API_KEY};\
        proxy_connect_timeout 30s;\
        proxy_send_timeout 120s;\
        proxy_read_timeout 120s;\
    }\
\
    location /analysis/ {\
        proxy_pass http://analysis-engine:8090;\
        proxy_http_version 1.1;\
        proxy_set_header Upgrade \$http_upgrade;\
        proxy_set_header Connection \"upgrade\";\
        proxy_set_header Host \$host;\
        proxy_set_header X-Real-IP \$remote_addr;\
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;\
        proxy_set_header X-Forwarded-Proto \$scheme;\
        proxy_read_timeout 300s;\
        proxy_connect_timeout 30s;\
        proxy_send_timeout 300s;\
    }\
}\
" > /etc/nginx/conf.d/default.conf

# Copy built files from builder
COPY --from=builder /app/dist /usr/share/nginx/html

# Expose port 3000
EXPOSE 3000

CMD ["nginx", "-g", "daemon off;"]
