FROM microsoft/aspnet:1.0.0-rc1-final

# Prevent `dnu restore` from stalling (gh#63, gh#80)
#ENV MONO_THREADS_PER_CPU 2000
EXPOSE 5050

# Copy 'dnu publish --configuration release' result
COPY bin/output /app
WORKDIR /app

RUN mv approot/kestrel approot/kestrel.sh && chmod +x approot/kestrel.sh

ENTRYPOINT ["./approot/kestrel.sh"]

