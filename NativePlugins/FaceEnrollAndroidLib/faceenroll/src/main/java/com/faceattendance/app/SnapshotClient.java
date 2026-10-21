package com.faceattendance.app;

import java.io.BufferedInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import javax.net.SocketFactory;

/**
 * Lấy ảnh tĩnh (JPEG) từ camera IP Hikvision qua HTTP: GET /ISAPI/Streaming/channels/101/picture.
 *
 * Tự viết HTTP + Digest (MD5) bằng Socket thuần để: (1) không phụ thuộc thư viện, chạy được cả trong
 * plugin Unity lẫn FaceEnroll, (2) gắn được socket vào đúng mạng Ethernet (xem {@link #bindTo} và
 * {@code Network.getSocketFactory()}), vì Android coi eth0 không có Internet nên socket mặc định có thể đi Wi-Fi.
 *
 * Nonce Digest được giữ lại giữa các lần gọi (nc tăng dần) nên chỉ lần đầu tốn thêm 1 vòng 401.
 * Chỉ hỗ trợ Digest/MD5 (không Basic, không MD5-sess). Không thread-safe theo từng lệnh: các hàm đã synchronized.
 */
public final class SnapshotClient {

    public static final String DEFAULT_PATH = "/ISAPI/Streaming/channels/101/picture";
    private static final int MAX_BODY = 16 * 1024 * 1024;
    private static final Pattern PARAM = Pattern.compile("(\\w+)=(?:\"([^\"]*)\"|([^,\\s]*))");

    private final String host;
    private final int port;
    private final String user;
    private final String pass;
    private final SocketFactory sockets;
    private final int timeoutMs;
    private final SecureRandom rnd = new SecureRandom();

    // Digest challenge đang giữ (null khi chưa có).
    private String realm;
    private String nonce;
    private String opaque;
    private boolean useQop;
    private int nc;

    /**
     * @param sockets nguồn socket (dùng {@code Network.getSocketFactory()} để gắn vào eth0); null = mặc định hệ thống.
     */
    public SnapshotClient(String host, int port, String user, String pass, SocketFactory sockets, int timeoutMs) {
        this.host = host;
        this.port = port;
        this.user = user == null ? "" : user;
        this.pass = pass == null ? "" : pass;
        this.sockets = sockets != null ? sockets : SocketFactory.getDefault();
        this.timeoutMs = timeoutMs > 0 ? timeoutMs : 5000;
    }

    /** Socket bind sẵn vào địa chỉ cục bộ {@code localIp} (ép đi qua giao diện có IP đó). Dùng khi không có {@code Network}. */
    public static SocketFactory bindTo(final String localIp) {
        final SocketFactory base = SocketFactory.getDefault();
        return new SocketFactory() {
            @Override public Socket createSocket() throws IOException {
                Socket s = new Socket();
                s.bind(new InetSocketAddress(InetAddress.getByName(localIp), 0));
                return s;
            }
            @Override public Socket createSocket(String h, int p) throws IOException { return base.createSocket(h, p); }
            @Override public Socket createSocket(String h, int p, InetAddress la, int lp) throws IOException { return base.createSocket(h, p, la, lp); }
            @Override public Socket createSocket(InetAddress h, int p) throws IOException { return base.createSocket(h, p); }
            @Override public Socket createSocket(InetAddress h, int p, InetAddress la, int lp) throws IOException { return base.createSocket(h, p, la, lp); }
        };
    }

    /** Chụp 1 ảnh JPEG tại đường dẫn {@code path} (mặc định {@link #DEFAULT_PATH}). */
    public synchronized byte[] fetch(String path) throws IOException {
        if (path == null || path.isEmpty()) path = DEFAULT_PATH;
        Response r = request(path, authHeader(path));
        if (r.status == 401) {
            if (!parseChallenge(r.wwwAuth)) throw new IOException("HTTP 401 nhưng không có thử thách Digest (camera đang dùng Basic?)");
            // Không có mật khẩu thì dừng ở đây: không gửi thử sai, vì Hikvision khoá IP sau vài lần sai.
            if (pass.isEmpty()) throw new IOException("Cần mật khẩu (realm=" + realm + ")");
            r = request(path, authHeader(path));
        }
        if (r.status == 401) throw new IOException("HTTP 401: sai tài khoản/mật khẩu (dừng, không thử lại để khỏi bị khoá IP)");
        if (r.status != 200) throw new IOException("HTTP " + r.status);
        return r.body;
    }

    // ── HTTP ────────────────────────────────────────────────────────────────

    private static final class Response {
        int status;
        List<String> wwwAuth = new ArrayList<String>();
        byte[] body = new byte[0];
    }

    private Response request(String path, String auth) throws IOException {
        Socket s = sockets.createSocket();
        try {
            s.connect(new InetSocketAddress(host, port), timeoutMs);
            s.setSoTimeout(timeoutMs);
            StringBuilder sb = new StringBuilder();
            sb.append("GET ").append(path).append(" HTTP/1.1\r\n")
              .append("Host: ").append(host).append(':').append(port).append("\r\n")
              .append("Accept: */*\r\nConnection: close\r\n");
            if (auth != null) sb.append("Authorization: ").append(auth).append("\r\n");
            sb.append("\r\n");
            OutputStream out = s.getOutputStream();
            out.write(sb.toString().getBytes("US-ASCII"));
            out.flush();

            InputStream in = new BufferedInputStream(s.getInputStream(), 16 * 1024);
            String status = readLine(in);
            if (status == null || !status.startsWith("HTTP/")) throw new IOException("Phản hồi không phải HTTP: " + status);
            String[] sp = status.split(" ", 3);
            Response r = new Response();
            r.status = Integer.parseInt(sp[1].trim());

            long contentLength = -1;
            boolean chunked = false;
            String line;
            while ((line = readLine(in)) != null && !line.isEmpty()) {
                int c = line.indexOf(':');
                if (c < 0) continue;
                String k = line.substring(0, c).trim().toLowerCase(Locale.US);
                String v = line.substring(c + 1).trim();
                if (k.equals("www-authenticate")) r.wwwAuth.add(v);
                else if (k.equals("content-length")) { try { contentLength = Long.parseLong(v); } catch (NumberFormatException ignored) { } }
                else if (k.equals("transfer-encoding") && v.toLowerCase(Locale.US).contains("chunked")) chunked = true;
            }
            if (r.status == 200 || contentLength != 0) r.body = readBody(in, contentLength, chunked);
            return r;
        } finally {
            try { s.close(); } catch (IOException ignored) { }
        }
    }

    private static String readLine(InputStream in) throws IOException {
        StringBuilder sb = new StringBuilder();
        int b;
        while ((b = in.read()) != -1) {
            if (b == '\n') break;
            if (b != '\r') sb.append((char) b);
            if (sb.length() > 8192) throw new IOException("Dòng header quá dài");
        }
        if (b == -1 && sb.length() == 0) return null;
        return sb.toString();
    }

    private static byte[] readBody(InputStream in, long contentLength, boolean chunked) throws IOException {
        ByteArrayOutputStream bo = new ByteArrayOutputStream(contentLength > 0 ? (int) Math.min(contentLength, MAX_BODY) : 64 * 1024);
        byte[] buf = new byte[16 * 1024];
        if (chunked) {
            while (true) {
                String sz = readLine(in);
                if (sz == null) throw new IOException("Chunk bị cắt");
                int semi = sz.indexOf(';');
                if (semi >= 0) sz = sz.substring(0, semi);
                int n = Integer.parseInt(sz.trim(), 16);
                if (n == 0) break;
                copy(in, bo, n, buf);
                readLine(in); // CRLF sau chunk
            }
        } else if (contentLength >= 0) {
            copy(in, bo, contentLength, buf);
        } else {
            int n;
            while ((n = in.read(buf)) != -1) {
                bo.write(buf, 0, n);
                if (bo.size() > MAX_BODY) throw new IOException("Body quá lớn");
            }
        }
        return bo.toByteArray();
    }

    private static void copy(InputStream in, ByteArrayOutputStream bo, long n, byte[] buf) throws IOException {
        if (n > MAX_BODY) throw new IOException("Body quá lớn: " + n);
        long left = n;
        while (left > 0) {
            int r = in.read(buf, 0, (int) Math.min(buf.length, left));
            if (r < 0) throw new IOException("Mất kết nối khi đang nhận (" + (n - left) + "/" + n + " byte)");
            bo.write(buf, 0, r);
            left -= r;
        }
    }

    // ── Digest ──────────────────────────────────────────────────────────────

    private boolean parseChallenge(List<String> headers) throws IOException {
        for (String h : headers) {
            if (!h.regionMatches(true, 0, "Digest", 0, 6)) continue;
            String realmV = null, nonceV = null, opaqueV = null, qopV = null, algV = null;
            Matcher m = PARAM.matcher(h.substring(6));
            while (m.find()) {
                String k = m.group(1).toLowerCase(Locale.US);
                String v = m.group(2) != null ? m.group(2) : m.group(3);
                if (k.equals("realm")) realmV = v;
                else if (k.equals("nonce")) nonceV = v;
                else if (k.equals("opaque")) opaqueV = v;
                else if (k.equals("qop")) qopV = v;
                else if (k.equals("algorithm")) algV = v;
            }
            if (realmV == null || nonceV == null) continue;
            if (algV != null && !algV.equalsIgnoreCase("MD5")) throw new IOException("Digest algorithm không hỗ trợ: " + algV);
            realm = realmV;
            nonce = nonceV;
            opaque = opaqueV;
            useQop = qopV != null && (" " + qopV.replace(',', ' ') + " ").contains(" auth ");
            nc = 0;
            return true;
        }
        return false;
    }

    private String authHeader(String path) throws IOException {
        if (nonce == null || pass.isEmpty()) return null;
        nc++;
        String ncStr = String.format(Locale.US, "%08x", nc);
        String cnonce = hex(randomBytes(8));
        String ha1 = md5(user + ":" + realm + ":" + pass);
        String ha2 = md5("GET:" + path);
        String response = useQop
                ? md5(ha1 + ":" + nonce + ":" + ncStr + ":" + cnonce + ":auth:" + ha2)
                : md5(ha1 + ":" + nonce + ":" + ha2);
        StringBuilder sb = new StringBuilder("Digest username=\"").append(user)
                .append("\", realm=\"").append(realm)
                .append("\", nonce=\"").append(nonce)
                .append("\", uri=\"").append(path)
                .append("\", algorithm=MD5, response=\"").append(response).append('"');
        if (useQop) sb.append(", qop=auth, nc=").append(ncStr).append(", cnonce=\"").append(cnonce).append('"');
        if (opaque != null) sb.append(", opaque=\"").append(opaque).append('"');
        return sb.toString();
    }

    private byte[] randomBytes(int n) {
        byte[] b = new byte[n];
        rnd.nextBytes(b);
        return b;
    }

    private static String md5(String s) throws IOException {
        try {
            return hex(MessageDigest.getInstance("MD5").digest(s.getBytes("UTF-8")));
        } catch (java.security.NoSuchAlgorithmException e) {
            throw new IOException(e);
        }
    }

    private static String hex(byte[] b) {
        char[] d = "0123456789abcdef".toCharArray();
        char[] o = new char[b.length * 2];
        for (int i = 0; i < b.length; i++) {
            o[i * 2] = d[(b[i] >> 4) & 0xF];
            o[i * 2 + 1] = d[b[i] & 0xF];
        }
        return new String(o);
    }
}
