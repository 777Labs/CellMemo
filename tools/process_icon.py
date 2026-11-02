import os
import sys
import cv2
import numpy as np
from scipy.ndimage import gaussian_filter1d
from PIL import Image


def cv2_imread_unicode(path: str, flags: int = cv2.IMREAD_UNCHANGED) -> np.ndarray:
    """Windows non-ASCII (日本語) パス対応の cv2.imread"""
    with open(path, "rb") as f:
        bytes_data = bytearray(f.read())
        numpy_array = np.asarray(bytes_data, dtype=np.uint8)
        return cv2.imdecode(numpy_array, flags)


def cv2_imwrite_unicode(path: str, img: np.ndarray) -> bool:
    """Windows non-ASCII (日本語) パス対応の cv2.imwrite"""
    ext = os.path.splitext(path)[1]
    result, encimg = cv2.imencode(ext, img)
    if result:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as f:
            f.write(encimg)
        return True
    return False


def create_procedural_raw_icon(output_path: str, size: int = 1024) -> None:
    """
    assets/raw_icon.png が存在しない場合に、添付UIモックアップの左上のアイコンデザインに完全準拠した
    『スパイラルリング付きブルー＆グリッドメモ帳』の原画（1024x1024）を生成する。
    """
    img = np.zeros((size, size, 4), dtype=np.uint8)

    # ノート本体の座標定義（角丸長方形シルエット、背景プレート枠なし）
    x1, y1, x2, y2 = 140, 110, 884, 950
    radius = 64

    # アルファマスクとRGBキャンバスの作成
    mask = np.zeros((size, size), dtype=np.uint8)
    cv2.rectangle(mask, (x1 + radius, y1), (x2 - radius, y2), 255, -1)
    cv2.rectangle(mask, (x1, y1 + radius), (x2, y2 - radius), 255, -1)
    cv2.circle(mask, (x1 + radius, y1 + radius), radius, 255, -1)
    cv2.circle(mask, (x2 - radius, y1 + radius), radius, 255, -1)
    cv2.circle(mask, (x1 + radius, y2 - radius), radius, 255, -1)
    cv2.circle(mask, (x2 - radius, y2 - radius), radius, 255, -1)

    # 紙面ベースカラー（爽やかなライトシアン〜ホワイトグラデーション）
    for y in range(y1, y2):
        ratio = (y - y1) / max(1, (y2 - y1))
        r = int(245 - 15 * ratio)
        g = int(250 - 10 * ratio)
        b = int(255 - 5 * ratio)
        img[y, x1:x2, 0] = b
        img[y, x1:x2, 1] = g
        img[y, x1:x2, 2] = r

    # 下部エッジの立体ページ厚み表現（ダークティール）
    img[y2 - 36:y2, x1:x2, 0] = 150
    img[y2 - 36:y2, x1:x2, 1] = 110
    img[y2 - 36:y2, x1:x2, 2] = 40

    # 上部ヘッダーバンド（Windows 11 Notepad ブルー）
    header_bottom = y1 + 170
    for y in range(y1, header_bottom):
        img[y, x1:x2, 0] = 212  # B
        img[y, x1:x2, 1] = 120  # G
        img[y, x1:x2, 2] = 0    # R (#0078D4)

    # グリッド線の描画（Excel風セル構造）
    grid_top = header_bottom + 10
    grid_bottom = y2 - 36
    col_widths = [186, 372, 558]
    for cw in col_widths:
        gx = x1 + cw
        cv2.line(img, (gx, grid_top), (gx, grid_bottom), (210, 200, 180, 255), 10)

    row_heights = [125, 250, 375, 500]
    for rh in row_heights:
        gy = grid_top + rh
        if gy < grid_bottom:
            cv2.line(img, (x1 + 24, gy), (x2 - 24, gy), (195, 160, 90, 255), 12)

    # 左上アクティブセルの緑色太枠ハイライト (#107C41)
    cell_x1, cell_y1 = x1 + 32, grid_top + 20
    cell_x2, cell_y2 = x1 + col_widths[0] - 12, grid_top + row_heights[0] - 12
    cv2.rectangle(img, (cell_x1, cell_y1), (cell_x2, cell_y2), (230, 244, 234, 255), -1)
    cv2.rectangle(img, (cell_x1, cell_y1), (cell_x2, cell_y2), (65, 124, 16, 255), 16)

    # 上部スパイラルリング（5個）の描画とマスク統合
    ring_xs = [235, 373, 512, 650, 788]
    for rx in ring_xs:
        ry1, ry2 = y1 - 42, y1 + 48
        cv2.rectangle(mask, (rx - 22, ry1), (rx + 22, ry2), 255, -1)
        cv2.circle(mask, (rx, ry1), 22, 255, -1)
        cv2.circle(mask, (rx, ry2), 22, 255, -1)
        cv2.rectangle(img, (rx - 22, ry1), (rx + 22, ry2), (110, 115, 120, 255), -1)
        cv2.circle(img, (rx, ry1), 22, (140, 145, 150, 255), -1)
        cv2.circle(img, (rx, ry2), 22, (80, 85, 90, 255), -1)

    img[:, :, 3] = mask
    cv2_imwrite_unicode(output_path, img)


def resample_closed_contour(contour: np.ndarray, num_points: int = 3300) -> np.ndarray:
    """
    輪郭座標列を閉曲線として周長に沿った等間隔な num_points 点（デフォルト3,300点）にリサンプリングする。
    """
    pts = contour.reshape(-1, 2).astype(np.float64)
    closed_pts = np.vstack([pts, pts[0]])
    diffs = np.diff(closed_pts, axis=0)
    segment_lengths = np.hypot(diffs[:, 0], diffs[:, 1])
    cumulative_length = np.concatenate([[0.0], np.cumsum(segment_lengths)])
    total_length = cumulative_length[-1]

    if total_length <= 1e-6:
        return pts

    target_distances = np.linspace(0.0, total_length, num_points, endpoint=False)
    resampled_x = np.interp(target_distances, cumulative_length, closed_pts[:, 0])
    resampled_y = np.interp(target_distances, cumulative_length, closed_pts[:, 1])
    return np.column_stack([resampled_x, resampled_y])


def process_icon_pipeline(raw_png_path: str, master_png_path: str, ico_path: str) -> None:
    """
    3段階の幾何学・超解像アンチエイリアシング処理およびPremultiplied AlphaマルチサイズICO変換を実行する。
    """
    if not os.path.exists(raw_png_path):
        create_procedural_raw_icon(raw_png_path, 1024)

    src_bgra = cv2_imread_unicode(raw_png_path, cv2.IMREAD_UNCHANGED)
    if src_bgra is None:
        raise FileNotFoundError(f"Failed to load image: {raw_png_path}")

    # 入力が3チャンネル（白背景RGB）の場合は背景を透過アルファに変換
    if src_bgra.shape[2] == 3:
        bgr = src_bgra
        gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
        _, alpha = cv2.threshold(gray, 250, 255, cv2.THRESH_BINARY_INV)
        src_bgra = np.dstack([bgr, alpha])

    if src_bgra.shape[0] != 1024 or src_bgra.shape[1] != 1024:
        src_bgra = cv2.resize(src_bgra, (1024, 1024), interpolation=cv2.INTER_LANCZOS4)

    alpha_channel = src_bgra[:, :, 3]
    bgr_channel = src_bgra[:, :, :3]

    # =========================================================================
    # Stage 1: ベクタースプライン座標平滑化 (3,300点等間隔リサンプリング + 周期ガウシアン平滑化)
    # =========================================================================
    _, binary_mask = cv2.threshold(alpha_channel, 127, 255, cv2.THRESH_BINARY)
    contours, _ = cv2.findContours(binary_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    if not contours:
        raise ValueError("No valid foreground contour found in alpha channel.")

    smoothed_contours = []
    all_pts_list = []

    for cnt in contours:
        if cv2.contourArea(cnt) < 100.0:
            continue
        resampled = resample_closed_contour(cnt, num_points=3300)
        # 周期ガウシアンフィルター (mode='wrap') でピクセル格子の微小な階段ノイズを完全平滑化
        smooth_x = gaussian_filter1d(resampled[:, 0], sigma=4.5, mode='wrap')
        smooth_y = gaussian_filter1d(resampled[:, 1], sigma=4.5, mode='wrap')
        smooth_pts = np.column_stack([smooth_x, smooth_y])
        smoothed_contours.append(smooth_pts)
        all_pts_list.append(smooth_pts)

    all_pts = np.vstack(all_pts_list)

    # =========================================================================
    # Stage 2: ベクター段階でのフルブリード拡大 (キャンバス占有率 96.5% = 988px, 四辺約18pxマージン)
    # =========================================================================
    canvas_size = 1024.0
    target_occupancy = 0.965
    target_span = canvas_size * target_occupancy  # 988.16 px

    min_xy = np.min(all_pts, axis=0)
    max_xy = np.max(all_pts, axis=0)
    bbox_size = max_xy - min_xy
    max_dim = max(float(bbox_size[0]), float(bbox_size[1]), 1.0)

    scale = target_span / max_dim
    src_center = (min_xy + max_xy) * 0.5
    dst_center = np.array([canvas_size * 0.5, canvas_size * 0.5])

    # ベクター座標の正規化・フルブリード拡大変換
    normalized_contours = []
    for s_pts in smoothed_contours:
        norm_pts = (s_pts - src_center) * scale + dst_center
        normalized_contours.append(norm_pts)

    # 内部RGBカラー画像の背景色フチ滲み防止（前景色のモルフォロジー膨張による境界外挿）
    fg_mask = (alpha_channel > 128).astype(np.uint8)
    dilated_bgr = bgr_channel.copy()
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))
    for _ in range(8):
        expanded_bgr = cv2.dilate(dilated_bgr, kernel)
        dilated_bgr = np.where(fg_mask[:, :, None] == 1, dilated_bgr, expanded_bgr)
        fg_mask = cv2.dilate(fg_mask, kernel)

    # ベクターと同一のアフィン変換行列で内部RGBカラー画像を変換
    tx = dst_center[0] - src_center[0] * scale
    ty = dst_center[1] - src_center[1] * scale
    affine_matrix = np.array([
        [scale, 0.0, tx],
        [0.0, scale, ty]
    ], dtype=np.float64)

    warped_bgr = cv2.warpAffine(
        dilated_bgr,
        affine_matrix,
        (1024, 1024),
        flags=cv2.INTER_LANCZOS4,
        borderMode=cv2.BORDER_REPLICATE
    )

    # =========================================================================
    # Stage 3: 8倍スーパーサンプリング (8x SSAA: 8192x8192) によるピクセル面積積分
    # =========================================================================
    ssaa_factor = 8
    ssaa_size = 1024 * ssaa_factor  # 8,192 x 8,192 px
    ssaa_mask = np.zeros((ssaa_size, ssaa_size), dtype=np.uint8)

    ssaa_polygons = [
        np.round(norm_pts * ssaa_factor).astype(np.int32).reshape((-1, 1, 2))
        for norm_pts in normalized_contours
    ]
    cv2.fillPoly(ssaa_mask, ssaa_polygons, 255, lineType=cv2.LINE_AA)

    # INTER_AREAによる厳密なピクセル面積積分（1出力ピクセルあたり64サブピクセル積分）
    smooth_alpha = cv2.resize(
        ssaa_mask,
        (1024, 1024),
        interpolation=cv2.INTER_AREA
    )

    master_bgra = np.dstack([warped_bgr, smooth_alpha])
    cv2_imwrite_unicode(master_png_path, master_bgra)

    # =========================================================================
    # Stage 4: Premultiplied Alpha 空間でのマルチサイズ icon.ico 生成
    # =========================================================================
    ico_sizes = [256, 64, 48, 32, 24, 16]
    pil_images = []

    master_rgba = cv2.cvtColor(master_bgra, cv2.COLOR_BGRA2RGBA).astype(np.float32) / 255.0
    alpha_f = master_rgba[:, :, 3:4]
    premultiplied_rgb = master_rgba[:, :, :3] * alpha_f
    premultiplied_rgba = np.concatenate([premultiplied_rgb, alpha_f], axis=2)

    for target_sz in ico_sizes:
        down_area = cv2.resize(premultiplied_rgba, (target_sz, target_sz), interpolation=cv2.INTER_AREA)
        down_lanczos = cv2.resize(premultiplied_rgba, (target_sz, target_sz), interpolation=cv2.INTER_LANCZOS4)
        down_pm = np.clip(down_area * 0.65 + down_lanczos * 0.35, 0.0, 1.0)

        out_alpha = down_pm[:, :, 3:4]
        safe_alpha = np.where(out_alpha > 1e-4, out_alpha, 1.0)
        unpremultiplied_rgb = np.where(out_alpha > 1e-4, down_pm[:, :, :3] / safe_alpha, 0.0)
        unpremultiplied_rgb = np.clip(unpremultiplied_rgb, 0.0, 1.0)

        final_rgba_u8 = np.round(np.concatenate([unpremultiplied_rgb, out_alpha], axis=2) * 255.0).astype(np.uint8)
        pil_img = Image.fromarray(final_rgba_u8, mode="RGBA")
        pil_images.append(pil_img)

    os.makedirs(os.path.dirname(ico_path), exist_ok=True)
    pil_images[0].save(
        ico_path,
        format="ICO",
        sizes=[(sz, sz) for sz in ico_sizes],
        append_images=pil_images[1:]
    )
    print(f"[SUCCESS] Generated Master PNG: {master_png_path} and Multi-size ICO: {ico_path}")


if __name__ == "__main__":
    base_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    raw_path = os.path.join(base_dir, "assets", "raw_icon.png")
    master_path = os.path.join(base_dir, "assets", "icon_master_1024.png")
    ico_output = os.path.join(base_dir, "assets", "icon.ico")
    process_icon_pipeline(raw_path, master_path, ico_output)
