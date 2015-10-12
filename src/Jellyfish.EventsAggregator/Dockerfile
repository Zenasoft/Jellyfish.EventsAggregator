FROM microsoft/aspnet:1.0.0-beta7

# Copy 'dnu publish' result
COPY bin/output /app
WORKDIR /app

RUN mv kestrel kestrel.sh && chmod +x kestrel.sh

ENTRYPOINT ["./kestrel.sh"]

