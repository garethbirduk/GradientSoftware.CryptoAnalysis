// A chart drawn on a canvas, given what Plotly is given: the same traces, shapes and annotations, through the same few
// calls the page makes (react, relayout, Plots.resize, the element's on, _fullLayout). Plotly keeps an element in the page
// for everything it draws and moves each of them at every pan and zoom, which is slow in the hundreds. Here a move paints
// the canvas again, and only what lies in view is painted, so its cost follows what can be seen and not what was given.
// It draws the kinds of thing the page's charts use, not all that Plotly can: candlesticks; scatter traces as lines,
// markers, text and filled outlines; rectangles and lines as shapes; texts and arrows as annotations.
window.CanvasChart = (() => {
  const times = new Map();
  // A time as milliseconds: a number as it is, a text read as UTC when it names no zone, as Plotly reads it.
  const ms = v => {
    if (typeof v === 'number') return v;
    if (v == null) return NaN;
    let t = times.get(v);
    if (t === undefined) {
      if (times.size > 400000) times.clear();
      const s = String(v);
      times.set(v, t = Date.parse(/[zZ]|[+-]\d\d:?\d\d$/.test(s) ? s : `${s.replace(' ', 'T')}Z`));
    }
    return t;
  };
  const iso = t => new Date(t).toISOString();
  const pick = (v, i) => Array.isArray(v) ? v[i] : v;
  const lines = text => String(text ?? '').replace(/<extra>.*?<\/extra>/gs, '').split(/<br\s*\/?>/i).map(s => s.replace(/<[^>]+>/g, ''));
  const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const two = n => String(n).padStart(2, '0');
  const HOUR = 3600000, DAY = 86400000;

  // A trace made ready to paint: its times and prices as numbers, the stretches between its gaps with the times each
  // spans, and whether its times only rise, when what is in view is found by halving and not by looking at every point.
  function prepare(trace) {
    // A trace can come with its times already as numbers (xms), which are then not read again from its texts.
    const n = trace.x?.length ?? 0, x = trace.xms?.length === n ? trace.xms : new Float64Array(n);
    if (x !== trace.xms) for (let i = 0; i < n; i++) x[i] = ms(trace.x[i]);
    if (trace.type === 'candlestick') return { trace, kind: 'candles', x, n, rising: true };
    const y = new Float64Array(n);
    for (let i = 0; i < n; i++) { const v = trace.y?.[i]; y[i] = v == null ? NaN : +v; }
    const runs = [];
    let rising = true, from = -1, low = 0, high = 0, bottom = 0, top = 0, last = -Infinity;
    for (let i = 0; i <= n; i++) {
      const gap = i === n || Number.isNaN(x[i]) || Number.isNaN(y[i]);
      if (gap) { if (from >= 0) runs.push({ from, to: i, low, high, bottom, top }); from = -1; continue; }
      if (from < 0) { from = i; low = high = x[i]; bottom = top = y[i]; }
      else { if (x[i] < low) low = x[i]; if (x[i] > high) high = x[i]; if (y[i] < bottom) bottom = y[i]; if (y[i] > top) top = y[i]; }
      if (x[i] < last) rising = false;
      last = x[i];
    }
    const mode = trace.mode ?? 'lines';
    return { trace, kind: 'scatter', x, y, n, runs, rising: rising && runs.length === 1,
      lines: /lines/.test(mode), markers: /markers/.test(mode), text: /text/.test(mode), filled: trace.fill === 'toself', paper: trace.yaxis === 'y2',
      hover: trace.hoverinfo !== 'skip' && trace.hoverinfo !== 'none' };
  }

  // Whether a box that asks for an edge in view (a trace's needsEdge) has none: the view lies wholly inside it, and all
  // there would be of it to paint is a tint over everything. It is then not painted, and has no hover; it comes back as
  // soon as a pan or zoom brings an edge of it into view, as this is asked at every paint.
  const allAround = (trace, run, x0, x1, y0, y1) => !!trace.needsEdge && run.low < x0 && run.high > x1 && run.bottom < Math.min(y0, y1) && run.top > Math.max(y0, y1);

  // The first index of rising times at or after a time.
  function firstAt(x, t, lo = 0, hi = x.length) {
    while (lo < hi) { const mid = (lo + hi) >> 1; if (x[mid] < t) lo = mid + 1; else hi = mid; }
    return lo;
  }

  // How strongly something is painted with a level in focus: in full when it is of that level, or of none, or when no
  // level is in focus; half as strong when of the level one coarser, whose Swings the level's are the interims of; faint
  // otherwise. A thing can be of several levels (lv a list), as a high shared by them is, and is as strong as its strongest.
  const FAINT = 0.13, PARENT = 0.45;
  function strength(focus, lv) {
    if (focus == null || lv == null) return 1;
    if (Array.isArray(lv)) return Math.max(...lv.map(l => strength(focus, l)));
    return lv === focus ? 1 : lv === focus - 1 ? PARENT : FAINT;
  }

  const DASH = { dash: w => [3 * w, 3 * w], dot: w => [w, w], dashdot: w => [3 * w, w, w, w], longdash: w => [5 * w, 3 * w] };
  function stroke(ctx, line, fallback) {
    const width = line?.width ?? 2;
    ctx.lineWidth = width;
    ctx.strokeStyle = line?.color ?? fallback ?? '#888';
    ctx.setLineDash(DASH[line?.dash]?.(Math.max(width, 3)) ?? []);
    return width > 0;
  }

  // A marker of Plotly's name at a point, its size across.
  function marker(ctx, symbol, x, y, size, color, edge, edgeWidth) {
    const open = /-open$/.test(symbol), name = symbol.replace(/-open$/, ''), r = size / 2;
    ctx.beginPath();
    let strokes = false;
    if (name === 'circle' || name === 'circle-x') ctx.arc(x, y, r, 0, 6.2832);
    else if (name === 'square') ctx.rect(x - r, y - r, size, size);
    else if (name === 'diamond') { ctx.moveTo(x, y - r * 1.3); ctx.lineTo(x + r * 1.3, y); ctx.lineTo(x, y + r * 1.3); ctx.lineTo(x - r * 1.3, y); ctx.closePath(); }
    else if (name === 'triangle-up') { ctx.moveTo(x, y - r * 1.15); ctx.lineTo(x + r * 1.15, y + r * 0.85); ctx.lineTo(x - r * 1.15, y + r * 0.85); ctx.closePath(); }
    else if (name === 'triangle-down') { ctx.moveTo(x, y + r * 1.15); ctx.lineTo(x + r * 1.15, y - r * 0.85); ctx.lineTo(x - r * 1.15, y - r * 0.85); ctx.closePath(); }
    else if (name === 'arrow-up') { ctx.moveTo(x, y - r * 1.2); ctx.lineTo(x + r, y + r); ctx.lineTo(x, y + r * 0.45); ctx.lineTo(x - r, y + r); ctx.closePath(); }
    else if (name === 'arrow-down') { ctx.moveTo(x, y + r * 1.2); ctx.lineTo(x + r, y - r); ctx.lineTo(x, y - r * 0.45); ctx.lineTo(x - r, y - r); ctx.closePath(); }
    else if (name === 'x' || name === 'x-thin') { ctx.moveTo(x - r, y - r); ctx.lineTo(x + r, y + r); ctx.moveTo(x + r, y - r); ctx.lineTo(x - r, y + r); strokes = true; }
    else ctx.arc(x, y, r, 0, 6.2832);
    ctx.setLineDash([]);
    if (strokes || open) {
      ctx.lineWidth = Math.max(edgeWidth ?? 0, strokes ? 2 : 1.5);
      ctx.strokeStyle = open ? color : edge ?? color;
      ctx.stroke();
    } else {
      ctx.fillStyle = color;
      ctx.fill();
      if (edgeWidth > 0) { ctx.lineWidth = edgeWidth; ctx.strokeStyle = edge ?? color; ctx.stroke(); }
    }
    if (name === 'circle-x') {
      const d = r * 0.7;
      ctx.beginPath(); ctx.moveTo(x - d, y - d); ctx.lineTo(x + d, y + d); ctx.moveTo(x + d, y - d); ctx.lineTo(x - d, y + d); ctx.stroke();
    }
  }

  // The times an axis marks between two, a step apart that leaves room for their labels, each with its label; the first
  // of a day or of a year says which under it.
  const STEPS = [HOUR, 2 * HOUR, 3 * HOUR, 6 * HOUR, 12 * HOUR, DAY, 2 * DAY, 4 * DAY, 7 * DAY, 14 * DAY];
  function timeTicks(from, to, width) {
    const most = Math.max(2, Math.floor(width / 100)), span = to - from, ticks = [];
    const step = STEPS.find(s => span / s <= most);
    if (step) {
      const first = Math.ceil(from / step) * step;
      let day = null;
      for (let t = first; t <= to; t += step) {
        const d = new Date(t), date = `${MONTHS[d.getUTCMonth()]} ${d.getUTCDate()}`, on = `${date}, ${d.getUTCFullYear()}`;
        ticks.push(step < DAY ? { t, text: `${two(d.getUTCHours())}:${two(d.getUTCMinutes())}`, under: on !== day ? on : '' }
          : { t, text: date, under: day === null || d.getUTCFullYear() !== day ? `${d.getUTCFullYear()}` : '' });
        day = step < DAY ? on : d.getUTCFullYear();
      }
      return ticks;
    }
    const months = [1, 2, 3, 6, 12, 24, 60, 120].find(m => span / (m * 30.44 * DAY) <= most) ?? 120;
    const a = new Date(from);
    let year = a.getUTCFullYear(), month = Math.ceil((a.getUTCMonth() + (a.getUTCDate() > 1 || a.getUTCHours() > 0 ? 1 : 0)) / months) * months, said = null;
    for (;;) {
      const t = Date.UTC(year, month, 1);
      if (t > to) break;
      const d = new Date(t);
      if (t >= from) {
        ticks.push(months >= 12 ? { t, text: `${d.getUTCFullYear()}`, under: '' }
          : { t, text: MONTHS[d.getUTCMonth()], under: d.getUTCFullYear() !== said ? `${d.getUTCFullYear()}` : '' });
        said = d.getUTCFullYear();
      }
      month += months;
    }
    return ticks;
  }

  function priceTicks(low, high, height) {
    const most = Math.max(2, Math.floor(height / 70)), rough = (high - low) / most;
    if (!(rough > 0)) return [];
    const power = Math.pow(10, Math.floor(Math.log10(rough))), step = [1, 2, 2.5, 5, 10].map(k => k * power).find(s => s >= rough);
    const digits = Math.max(0, -Math.floor(Math.log10(step) + 1e-9)), ticks = [];
    for (let p = Math.ceil(low / step) * step; p <= high; p += step) ticks.push({ p, text: p.toLocaleString('en-US', { minimumFractionDigits: digits, maximumFractionDigits: digits }) });
    return ticks;
  }

  function paint(el) {
    const s = el._canvasChart, layout = s.layout, margin = { t: 10, r: 56, b: 36, l: 10, ...layout.margin };
    const width = el.clientWidth, height = el.clientHeight;
    if (!width || !height) return;
    const ratio = window.devicePixelRatio || 1, canvas = s.canvas;
    if (canvas.width !== Math.round(width * ratio) || canvas.height !== Math.round(height * ratio)) {
      canvas.width = Math.round(width * ratio);
      canvas.height = Math.round(height * ratio);
    }
    const ctx = s.ctx, L = margin.l, T = margin.t, W = Math.max(10, width - margin.l - margin.r), H = Math.max(10, height - margin.t - margin.b);
    const [x0, x1] = s.x, [y0, y1] = s.y;
    const X = t => L + (t - x0) / (x1 - x0) * W, Y = p => T + (1 - (p - y0) / (y1 - y0)) * H, P = v => T + (1 - v) * H;
    el._fullLayout = { xaxis: { range: [iso(x0), iso(x1)] }, yaxis: { range: [y0, y1] }, _size: { l: L, t: T, w: W, h: H } };
    s.plot = { L, T, W, H, X, Y };
    s.hits = [];
    const font = layout.font ?? {}, family = font.family ?? 'system-ui, sans-serif', muted = font.color ?? '#888';
    ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    ctx.clearRect(0, 0, width, height);
    ctx.lineJoin = 'round';

    // The grid and the axes' labels.
    const across = timeTicks(x0, x1, W), up = priceTicks(y0, y1, H), grid = layout.xaxis?.gridcolor ?? 'rgba(128,128,128,.2)';
    ctx.setLineDash([]);
    ctx.lineWidth = 1;
    ctx.strokeStyle = grid;
    ctx.beginPath();
    for (const k of across) { const x = Math.round(X(k.t)) + 0.5; ctx.moveTo(x, T); ctx.lineTo(x, T + H); }
    for (const k of up) { const y = Math.round(Y(k.p)) + 0.5; ctx.moveTo(L, y); ctx.lineTo(L + W, y); }
    ctx.moveTo(L, T + H + 0.5); ctx.lineTo(L + W, T + H + 0.5);
    ctx.moveTo(L + W + 0.5, T); ctx.lineTo(L + W + 0.5, T + H);
    ctx.stroke();
    ctx.fillStyle = muted;
    ctx.font = `${font.size ?? 12}px ${family}`;
    ctx.textBaseline = 'top';
    ctx.textAlign = 'center';
    for (const k of across) { ctx.fillText(k.text, X(k.t), T + H + 5); if (k.under) ctx.fillText(k.under, X(k.t), T + H + 20); }
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';
    for (const k of up) ctx.fillText(k.text, L + W + 6, Y(k.p));

    ctx.save();
    ctx.beginPath();
    ctx.rect(L, T, W, H);
    ctx.clip();
    const shapes = layout.shapes ?? [];
    for (const shape of shapes) if (shape.layer === 'below') paintShape(ctx, shape, X, Y, P, x0, x1);
    // With a level in focus, what is of that level alone is painted last, over the rest.
    const strong = t => s.focus != null && !Array.isArray(t.trace.lv) && t.trace.lv === s.focus;
    for (const front of s.focus == null ? [false] : [false, true]) for (const t of s.traces) {
      if (t.trace.visible === false || strong(t) !== front) continue;
      const base = (t.trace.opacity ?? 1) * (Array.isArray(t.trace.lv) ? 1 : strength(s.focus, t.trace.lv));
      ctx.globalAlpha = base;
      if (t.kind === 'candles') paintCandles(ctx, t, X, Y, x0, x1, W);
      else paintScatter(ctx, t, X, t.paper ? P : Y, x0, x1, W, family, base, s.focus, t.paper ? null : s.y);
    }
    ctx.globalAlpha = 1;
    for (const shape of shapes) if (shape.layer !== 'below') paintShape(ctx, shape, X, Y, P, x0, x1);
    for (const a of layout.annotations ?? []) {
      // The labels of the other levels are left out, as they are most of what crowds a chart.
      const k = strength(s.focus, a.lv);
      a._box = null;
      if (k >= FAINT * 2) paintAnnotation(ctx, s, a, X, Y, x0, x1, L, T, W, H, family, font, k);
    }
    ctx.restore();
    el._emit('plotly_afterplot');
  }

  function paintShape(ctx, shape, X, Y, P, x0, x1) {
    if (shape.visible === false) return;
    const a = ms(shape.x0), b = ms(shape.x1);
    if (shape.xref !== 'paper' && (Math.max(a, b) < x0 || Math.min(a, b) > x1)) return;
    const y = shape.yref === 'paper' ? P : Y, xa = X(a), xb = X(b), ya = y(shape.y0), yb = y(shape.y1);
    ctx.globalAlpha = shape.opacity ?? 1;
    if (shape.type === 'rect') {
      if (shape.fillcolor) { ctx.fillStyle = shape.fillcolor; ctx.fillRect(Math.min(xa, xb), Math.min(ya, yb), Math.abs(xb - xa), Math.abs(yb - ya)); }
      if (stroke(ctx, shape.line)) ctx.strokeRect(Math.min(xa, xb), Math.min(ya, yb), Math.abs(xb - xa), Math.abs(yb - ya));
    } else if (stroke(ctx, shape.line)) {
      ctx.beginPath(); ctx.moveTo(xa, ya); ctx.lineTo(xb, yb); ctx.stroke();
    }
    ctx.globalAlpha = 1;
  }

  function paintCandles(ctx, t, X, Y, x0, x1, W) {
    const { x, n, trace } = t;
    if (!n) return;
    const from = Math.max(0, firstAt(x, x0) - 1), to = Math.min(n, firstAt(x, x1) + 1);
    const apart = n > 1 ? (x[n - 1] - x[0]) / (n - 1) : HOUR, body = Math.max(1, apart / (x1 - x0) * W * 0.55);
    ctx.setLineDash([]);
    for (const [rising, style] of [[true, trace.increasing], [false, trace.decreasing]]) {
      const color = style?.line?.color ?? (rising ? '#3d9970' : '#ff4136');
      ctx.strokeStyle = color;
      ctx.lineWidth = style?.line?.width ?? 1;
      ctx.fillStyle = color;
      ctx.beginPath();
      const alpha = ctx.globalAlpha;
      const boxes = [];
      for (let i = from; i < to; i++) {
        const o = trace.open[i], c = trace.close[i];
        if ((c >= o) !== rising) continue;
        const cx = Math.round(X(x[i])) + 0.5;
        // A wick runs from the high to the body and from the body to the low, not through the body, which is drawn
        // half clear; a candle too narrow for a body is the one line from its high to its low.
        if (body >= 2) {
          const top = Math.min(Y(o), Y(c)), tall = Math.max(1, Math.abs(Y(o) - Y(c)));
          ctx.moveTo(cx, Y(trace.high[i])); ctx.lineTo(cx, top);
          ctx.moveTo(cx, top + tall); ctx.lineTo(cx, Y(trace.low[i]));
          boxes.push(cx - body / 2, top, body, tall);
        } else {
          ctx.moveTo(cx, Y(trace.high[i])); ctx.lineTo(cx, Y(trace.low[i]));
        }
      }
      ctx.stroke();
      ctx.globalAlpha = alpha * 0.5;
      for (let k = 0; k < boxes.length; k += 4) ctx.fillRect(boxes[k], boxes[k + 1], boxes[k + 2], boxes[k + 3]);
      ctx.globalAlpha = alpha;
      for (let k = 0; k < boxes.length; k += 4) ctx.strokeRect(boxes[k], boxes[k + 1], boxes[k + 2], boxes[k + 3]);
    }
  }

  function paintScatter(ctx, t, X, Y, x0, x1, W, family, base = 1, focus = null, view = null) {
    const { x, y, trace } = t, line = trace.line;
    // A trace whose points are of different levels (lv a list, one for each point) paints each as strongly as its own.
    const each1 = Array.isArray(trace.lv) && focus != null ? i => { ctx.globalAlpha = base * strength(focus, trace.lv[i]); } : null;
    if (t.lines || t.filled) {
      // A long line of rising times with many points to a pixel is painted as each pixel's lowest and highest.
      if (t.rising && !t.filled) {
        const run = t.runs[0], from = Math.max(run.from, firstAt(x, x0, run.from, run.to) - 1), to = Math.min(run.to, firstAt(x, x1, run.from, run.to) + 1);
        if (to - from >= 2 && stroke(ctx, line)) {
          ctx.beginPath();
          if (to - from > W * 3) {
            let column = -1, low = 0, high = 0, last = 0;
            for (let i = from; i < to; i++) {
              const px = Math.floor(X(x[i])), py = Y(y[i]);
              if (px !== column) {
                if (column >= -1e9 && i > from) { ctx.lineTo(column + 0.5, low); ctx.lineTo(column + 0.5, high); ctx.lineTo(column + 0.5, last); }
                column = px; low = high = py;
                if (i === from) ctx.moveTo(px + 0.5, py); else ctx.lineTo(px + 0.5, py);
              } else { if (py < low) low = py; if (py > high) high = py; }
              last = py;
            }
            ctx.lineTo(column + 0.5, low); ctx.lineTo(column + 0.5, high); ctx.lineTo(column + 0.5, last);
          } else {
            ctx.moveTo(X(x[from]), Y(y[from]));
            for (let i = from + 1; i < to; i++) ctx.lineTo(X(x[i]), Y(y[i]));
          }
          ctx.stroke();
        }
      } else {
        ctx.beginPath();
        let any = false;
        for (const run of t.runs) {
          if (run.high < x0 || run.low > x1 || run.to - run.from < 2 || (view && allAround(trace, run, x0, x1, view[0], view[1]))) continue;
          any = true;
          ctx.moveTo(X(x[run.from]), Y(y[run.from]));
          for (let i = run.from + 1; i < run.to; i++) ctx.lineTo(X(x[i]), Y(y[i]));
          if (t.filled) ctx.closePath();
        }
        if (any) {
          if (t.filled && trace.fillcolor) { ctx.fillStyle = trace.fillcolor; ctx.fill(); }
          if (t.lines && stroke(ctx, line)) ctx.stroke();
        }
      }
    }
    if (!t.markers && !t.text) return;
    const m = trace.marker ?? {}, below = /bottom/.test(trace.textposition ?? ''), reach = (pick(m.size, 0) ?? 6) / 2 + 3;
    const each = draw => {
      if (t.rising) { const run = t.runs[0]; for (let i = Math.max(run.from, firstAt(x, x0, run.from, run.to) - 1), to = Math.min(run.to, firstAt(x, x1, run.from, run.to) + 1); i < to; i++) draw(i); return; }
      for (const run of t.runs) { if (run.high < x0 || run.low > x1) continue; for (let i = run.from; i < run.to; i++) if (x[i] >= x0 && x[i] <= x1) draw(i); }
    };
    if (t.markers) each(i => { each1?.(i); marker(ctx, pick(m.symbol, i) ?? 'circle', X(x[i]), Y(y[i]), pick(m.size, i) ?? 6, pick(m.color, i) ?? line?.color ?? '#888', pick(m.line?.color, i), pick(m.line?.width, i) ?? 0); });
    if (t.text) {
      ctx.font = `${trace.textfont?.size ?? 12}px ${family}`;
      ctx.textAlign = 'center';
      ctx.textBaseline = below ? 'top' : 'bottom';
      each(i => {
        const text = pick(trace.text, i);
        if (!text || (each1 && strength(focus, trace.lv[i]) < FAINT * 2)) return;
        each1?.(i);
        ctx.fillStyle = trace.textfont?.color ?? '#888';
        ctx.fillText(text, X(x[i]), Y(y[i]) + (below ? reach : -reach));
      });
    }
    ctx.globalAlpha = base;
  }

  // A text in its box at a point, moved by its shift, or an arrow from one point to another. What has a hover is kept
  // with where it was painted, for the pointer to find.
  function paintAnnotation(ctx, s, a, X, Y, x0, x1, L, T, W, H, family, base, strong = 1) {
    if (a.visible === false) return;
    const onPaper = a.xref === 'paper', px = onPaper ? L + a.x * W : X(ms(a.x)), py = a.yref === 'paper' ? T + (1 - a.y) * H : Y(a.y);
    ctx.globalAlpha = (a.opacity ?? 1) * strong;
    if (a.showarrow && a.axref === 'x') {
      const tx = X(ms(a.ax)), ty = Y(a.ay);
      if (Math.max(px, tx) >= L && Math.min(px, tx) <= L + W) {
        const width = a.arrowwidth ?? 1.5, head = (3 * width + 3) * (a.arrowsize ?? 1), angle = Math.atan2(py - ty, px - tx);
        ctx.setLineDash([]);
        ctx.lineWidth = width;
        ctx.strokeStyle = ctx.fillStyle = a.arrowcolor ?? '#888';
        ctx.beginPath(); ctx.moveTo(tx, ty); ctx.lineTo(px, py); ctx.stroke();
        const tip = (x, y, at) => {
          ctx.beginPath(); ctx.moveTo(x, y);
          ctx.lineTo(x - head * Math.cos(at - 0.42), y - head * Math.sin(at - 0.42));
          ctx.lineTo(x - head * Math.cos(at + 0.42), y - head * Math.sin(at + 0.42));
          ctx.closePath(); ctx.fill();
        };
        if (a.arrowhead) tip(px, py, angle);
        if (a.startarrowhead) tip(tx, ty, angle + Math.PI);
        if (a.hovertext) s.hits.push({ line: [tx, ty, px, py], text: a.hovertext, color: a.arrowcolor });
      }
    }
    // An arrow whose tail is so many pixels from its point, as a pinned text's is: the text sits at the tail.
    const pinned = a.showarrow && a.axref !== 'x' && (a.ax || a.ay), bx = pinned ? px + (a.ax ?? 0) : px, by = pinned ? py + (a.ay ?? 0) : py;
    if (pinned && Math.max(px, bx) >= L && Math.min(px, bx) <= L + W) {
      const width = a.arrowwidth ?? 1.5, head = (3 * width + 3) * (a.arrowsize ?? 1), angle = Math.atan2(py - by, px - bx);
      ctx.setLineDash([]);
      ctx.lineWidth = width;
      ctx.strokeStyle = ctx.fillStyle = a.arrowcolor ?? '#888';
      ctx.beginPath(); ctx.moveTo(bx, by); ctx.lineTo(px, py); ctx.stroke();
      if (a.arrowhead ?? 1) {
        ctx.beginPath(); ctx.moveTo(px, py);
        ctx.lineTo(px - head * Math.cos(angle - 0.42), py - head * Math.sin(angle - 0.42));
        ctx.lineTo(px - head * Math.cos(angle + 0.42), py - head * Math.sin(angle + 0.42));
        ctx.closePath(); ctx.fill();
      }
    }
    const text = lines(a.text).filter((line, i, all) => line || all.length > 1);
    if (text.length && text.some(Boolean) && (onPaper || (bx >= L - 60 && bx <= L + W + 60))) {
      const size = a.font?.size ?? base.size ?? 12, pad = (a.borderpad ?? 1) + (a.borderwidth ?? 0) + 2, height = size * 1.25;
      ctx.font = `${size}px ${family}`;
      const wide = Math.max(...text.map(line => ctx.measureText(line).width)), w = wide + pad * 2, h = text.length * height + pad * 2 - 2;
      const cx = bx + (a.xshift ?? 0) - (a.xanchor === 'left' ? 0 : a.xanchor === 'right' ? w : w / 2);
      const cy = by - (a.yshift ?? 0) - (a.yanchor === 'top' ? 0 : a.yanchor === 'bottom' ? h : h / 2);
      if (a.bgcolor) { ctx.fillStyle = a.bgcolor; ctx.fillRect(cx, cy, w, h); }
      if (a.bordercolor && (a.borderwidth ?? 1) > 0) { ctx.setLineDash([]); ctx.lineWidth = a.borderwidth ?? 1; ctx.strokeStyle = a.bordercolor; ctx.strokeRect(cx, cy, w, h); }
      ctx.fillStyle = a.font?.color ?? base.color ?? '#888';
      ctx.textAlign = a.align === 'left' ? 'left' : a.align === 'right' ? 'right' : 'center';
      ctx.textBaseline = 'top';
      const tx = a.align === 'left' ? cx + pad : a.align === 'right' ? cx + w - pad : cx + w / 2;
      text.forEach((line, i) => ctx.fillText(line, tx, cy + pad + i * height));
      if (a.hovertext) s.hits.push({ box: [cx, cy, w, h], text: a.hovertext, color: a.bordercolor });
      a._box = [cx, cy, w, h];
    }
    ctx.globalAlpha = 1;
  }

  // What is under the pointer: the nearest point with a hover within reach, else the smallest filled outline around it,
  // else a text or an arrow there, else the candle of its time.
  function under(el, mx, my) {
    const s = el._canvasChart, { L, T, W, H, X, Y } = s.plot, [x0, x1] = s.x;
    if (mx < L || mx > L + W || my < T || my > T + H) return null;
    const time = x0 + (mx - L) / W * (x1 - x0), perPixel = (x1 - x0) / W, P = v => T + (1 - v) * H;
    let best = null, near = 100, around = null, area = Infinity, candle = null;
    for (const t of s.traces) {
      const trace = t.trace;
      if (trace.visible === false) continue;
      if (t.kind === 'candles') {
        if (trace.hoverinfo === 'skip' || !t.n) continue;
        const i = Math.min(t.n - 1, firstAt(t.x, time)), k = i > 0 && time - t.x[i - 1] < t.x[i] - time ? i - 1 : i;
        if (Math.abs(X(t.x[k]) - mx) <= 6 && my >= Y(trace.high[k]) - 3 && my <= Y(trace.low[k]) + 3)
          candle = { text: `${fmt(t.x[k])}<br>open ${num(trace.open[k])}<br>high ${num(trace.high[k])}<br>low ${num(trace.low[k])}<br>close ${num(trace.close[k])}`, color: null };
        continue;
      }
      if (!t.hover) continue;
      const y = t.paper ? P : Y, text = i => pick(trace.hovertemplate, i) ?? pick(trace.hovertext, i) ?? (trace.hoverinfo === 'x+y' ? `${fmt(t.x[i])}<br>${num(t.y[i])}` : trace.name);
      const color = trace.line?.color ?? pick(trace.marker?.color, 0);
      const reach = Math.max(8, (pick(trace.marker?.size, 0) ?? 6) / 2 + 3);
      const points = !t.filled || /points/.test(trace.hoveron ?? 'points') || !trace.hoveron;
      const look = i => {
        const d = Math.hypot(X(t.x[i]) - mx, y(t.y[i]) - my);
        if (d <= reach && d < near) { near = d; best = { text: text(i), color, x: t.x[i], y: t.y[i] }; }
      };
      if (points && t.rising) {
        const run = t.runs[0], at = firstAt(t.x, time - reach * perPixel, run.from, run.to), end = firstAt(t.x, time + reach * perPixel, run.from, run.to);
        for (let i = at; i < Math.min(run.to, end + 1); i++) look(i);
      }
      for (const run of t.rising ? [] : t.runs) {
        if (run.high < time - reach * perPixel || run.low > time + reach * perPixel || (!t.paper && allAround(trace, run, x0, x1, s.y[0], s.y[1]))) continue;
        if (points) for (let i = run.from; i < run.to; i++) look(i);
        if (!t.filled || !/fills/.test(trace.hoveron ?? '')) continue;
        let inside = false, size = 0;
        for (let i = run.from, j = run.to - 1; i < run.to; j = i++) {
          const xi = X(t.x[i]), yi = y(t.y[i]), xj = X(t.x[j]), yj = y(t.y[j]);
          if ((yi > my) !== (yj > my) && mx < (xj - xi) * (my - yi) / (yj - yi) + xi) inside = !inside;
          size += xj * yi - xi * yj;
        }
        if (inside && Math.abs(size) < area) { area = Math.abs(size); around = { text: text(run.from), color: trace.line?.width ? color : trace.fillcolor }; }
      }
    }
    if (best) return best;
    for (const hit of s.hits) {
      if (hit.box && mx >= hit.box[0] && mx <= hit.box[0] + hit.box[2] && my >= hit.box[1] && my <= hit.box[1] + hit.box[3]) return hit;
      if (hit.line) {
        const [ax, ay, bx, by] = hit.line, k = Math.max(0, Math.min(1, ((mx - ax) * (bx - ax) + (my - ay) * (by - ay)) / ((bx - ax) ** 2 + (by - ay) ** 2 || 1)));
        if (Math.hypot(ax + k * (bx - ax) - mx, ay + k * (by - ay) - my) <= 5) return hit;
      }
    }
    return around ?? candle;
  }

  const fmt = t => iso(t).slice(0, 16).replace('T', ' ');
  const num = v => typeof v === 'number' ? v.toLocaleString('en-US', { maximumFractionDigits: 2 }) : String(v);

  function hover(el, e) {
    const s = el._canvasChart, box = el.getBoundingClientRect(), mx = e.clientX - box.left, my = e.clientY - box.top;
    const hit = s.dragging ? null : under(el, mx, my);
    s.tip.hidden = !hit;
    if (!hit) return;
    s.tip.textContent = lines(String(hit.text).replace(/%\{x\}/g, hit.x != null ? fmt(hit.x) : '').replace(/%\{y\}/g, hit.y != null ? num(hit.y) : '')).join('\n');
    s.tip.style.borderColor = hit.color ?? 'var(--border, #888)';
    const w = s.tip.offsetWidth, h = s.tip.offsetHeight;
    s.tip.style.left = `${mx + 14 + w > el.clientWidth ? Math.max(0, mx - 14 - w) : mx + 14}px`;
    s.tip.style.top = `${Math.max(0, Math.min(el.clientHeight - h, my - h / 2))}px`;
  }

  function setUp(el, config) {
    el.textContent = '';
    if (getComputedStyle(el).position === 'static') el.style.position = 'relative';
    const canvas = document.createElement('canvas');
    canvas.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;display:block;touch-action:none';
    const tip = document.createElement('div');
    tip.hidden = true;
    tip.style.cssText = 'position:absolute;z-index:3;pointer-events:none;white-space:pre;font:12px system-ui,sans-serif;padding:4px 7px;border-radius:5px;' +
      'border:1.5px solid;background:var(--panel,#fff);color:var(--text,#222);box-shadow:0 1px 4px rgba(0,0,0,.18)';
    el.append(canvas, tip);
    const handlers = {};
    el.on = (name, fn) => { (handlers[name] ??= []).push(fn); return el; };
    el._emit = (name, data) => { for (const fn of handlers[name] ?? []) fn(data); };
    const s = el._canvasChart = { canvas, ctx: canvas.getContext('2d'), tip, traces: [], layout: {}, x: [0, 1], y: [0, 1], hits: [], dragging: null };
    for (const button of config?.modeBarButtonsToAdd ?? []) {
      const b = document.createElement('button');
      b.type = 'button';
      b.title = b.textContent = button.name;
      b.style.cssText = 'position:absolute;z-index:2;top:2px;right:60px;font:600 12px system-ui,sans-serif;padding:3px 10px;border:1px solid var(--accent,#888);border-radius:5px;' +
        'background:var(--panel,#fff);color:var(--accent,#444);cursor:pointer';
      b.addEventListener('click', () => button.click(el));
      el.append(b);
    }

    // A drag over the prices' part of the chart moves the times shown, painted at once and told as Plotly tells a pan.
    canvas.addEventListener('pointerdown', e => {
      const box = el.getBoundingClientRect(), p = s.plot, mx = e.clientX - box.left, my = e.clientY - box.top;
      if (e.button !== 0 || !p || mx < p.L || mx > p.L + p.W || my < p.T || my > p.T + p.H) return;
      s.dragging = { at: e.clientX, x: s.x.slice(), moved: false };
      canvas.setPointerCapture(e.pointerId);
    });
    canvas.addEventListener('pointermove', e => {
      const d = s.dragging;
      if (!d) return hover(el, e);
      if (!d.moved && Math.abs(e.clientX - d.at) < 3) return;
      d.moved = true;
      s.tip.hidden = true;
      canvas.style.cursor = 'grabbing';
      const shift = -(e.clientX - d.at) / s.plot.W * (d.x[1] - d.x[0]);
      s.x = [d.x[0] + shift, d.x[1] + shift];
      paint(el);
      el._emit('plotly_relayout', { 'xaxis.range[0]': iso(s.x[0]), 'xaxis.range[1]': iso(s.x[1]) });
    });
    const drop = e => { if (s.dragging) { s.dragging = null; canvas.style.cursor = ''; canvas.releasePointerCapture?.(e.pointerId); } };
    canvas.addEventListener('pointerup', drop);
    canvas.addEventListener('pointercancel', drop);
    canvas.addEventListener('pointerleave', () => { if (!s.dragging) tip.hidden = true; });
    canvas.addEventListener('dblclick', e => {
      const box = el.getBoundingClientRect(), p = s.plot, mx = e.clientX - box.left;
      if (p && mx >= p.L && mx <= p.L + p.W) el._emit('plotly_doubleclick');
    });
    if (config?.responsive !== false && window.ResizeObserver) new ResizeObserver(() => { if (el.isConnected && s.traces) paint(el); }).observe(el);
    return s;
  }

  // Draws a chart in an element: what Plotly.react does for the page.
  function react(el, traces, layout = {}, config) {
    const s = el._canvasChart ?? setUp(el, config);
    s.traces = traces.map(prepare);
    s.layout = layout;
    el.data = traces;
    el.layout = layout;
    if (layout.xaxis?.range) s.x = layout.xaxis.range.map(ms);
    if (layout.yaxis?.range) s.y = layout.yaxis.range.map(Number);
    s.focus = layout.focusLevel ?? null;
    s.tip.hidden = true;
    paint(el);
    return Promise.resolve(el);
  }

  // Moves a chart's times and prices shown: what Plotly.relayout does for the page.
  function relayout(el, update) {
    const s = el._canvasChart;
    if (!s) return Promise.resolve(el);
    const x = update['xaxis.range'] ?? (update['xaxis.range[0]'] !== undefined ? [update['xaxis.range[0]'], update['xaxis.range[1]']] : null);
    const y = update['yaxis.range'] ?? (update['yaxis.range[0]'] !== undefined ? [update['yaxis.range[0]'], update['yaxis.range[1]']] : null);
    if (x) s.x = x.map(ms);
    if (y) s.y = y.map(Number);
    paint(el);
    el._emit('plotly_relayout', update);
    return Promise.resolve(el);
  }

  // Puts a sawtooth level in focus, or none: what is of the other levels is painted faint (see strength).
  function focus(el, level) {
    const s = el._canvasChart;
    if (!s || s.focus === (level ?? null)) return;
    s.focus = level ?? null;
    paint(el);
  }

  // The sawtooth level of what is at a place on the page, when something of a level is: a marker or a label there, or
  // a line or the edge of a box within a few pixels; the nearest when there are several. Inside a box is not on it, as
  // boxes cover most of a chart. Something of several levels gives the one in focus when it is among them, else its first.
  function levelAt(el, clientX, clientY) {
    const s = el._canvasChart, p = s?.plot;
    if (!p) return null;
    const box = el.getBoundingClientRect(), mx = clientX - box.left, my = clientY - box.top, { L, T, W, H, X, Y } = p, [x0, x1] = s.x;
    if (mx < L || mx > L + W || my < T || my > T + H) return null;
    const one = lv => Array.isArray(lv) ? (lv.includes(s.focus) ? s.focus : lv[0]) : lv;
    for (const a of s.layout.annotations ?? []) {
      if (a.lv == null || a.showarrow || !a._box) continue;
      if (mx >= a._box[0] && mx <= a._box[0] + a._box[2] && my >= a._box[1] && my <= a._box[1] + a._box[3]) return one(a.lv);
    }
    const P = v => T + (1 - v) * H, perPixel = (x1 - x0) / W, time = x0 + (mx - L) / W * perPixel * W;
    let level = null, near = 6;
    for (const t of s.traces) {
      const trace = t.trace;
      if (t.kind !== 'scatter' || trace.lv == null || trace.visible === false) continue;
      const y = t.paper ? P : Y, each = Array.isArray(trace.lv);
      for (const run of t.runs) {
        if (run.high < time - 12 * perPixel || run.low > time + 12 * perPixel) continue;
        for (let i = run.from; i < run.to; i++) {
          const ax = X(t.x[i]), ay = y(t.y[i]);
          if (t.markers) {
            const d = Math.hypot(ax - mx, ay - my) - (pick(trace.marker?.size, i) ?? 6) / 2;
            if (d < near) { near = Math.max(0, d); level = one(each ? trace.lv[i] : trace.lv); }
          }
          if (i + 1 >= run.to || !(t.lines || t.filled) || t.paper) continue;
          const bx = X(t.x[i + 1]), by = y(t.y[i + 1]), k = Math.max(0, Math.min(1, ((mx - ax) * (bx - ax) + (my - ay) * (by - ay)) / ((bx - ax) ** 2 + (by - ay) ** 2 || 1)));
          const d = Math.hypot(ax + k * (bx - ax) - mx, ay + k * (by - ay) - my);
          if (d < near) { near = d; level = one(trace.lv); }
        }
      }
    }
    return level;
  }

  return { react, relayout, focus, levelAt, Plots: { resize: el => { if (el._canvasChart) paint(el); } } };
})();
