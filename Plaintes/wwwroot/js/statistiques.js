// Graphiques de la page Statistiques : tout est dessiné en SVG / HTML, sans librairie externe.
(function () {
    const donnees = document.getElementById('donneesStats');
    if (!donnees) return;
    const d = JSON.parse(donnees.textContent);

    const NS = 'http://www.w3.org/2000/svg';
    const VERT = '#78be20', BLEU = '#2d5068';
    const COULEURS = ['#78be20', '#2d5068', '#1f9d8a', '#f39c12', '#8e6bbf', '#d9534f', '#4aa3df', '#a0aab4'];
    const fr = new Intl.NumberFormat('fr-FR');

    // Crée un élément SVG avec ses attributs
    function svg(nom, attrs, parent) {
        const e = document.createElementNS(NS, nom);
        for (const k in attrs) e.setAttribute(k, attrs[k]);
        if (parent) parent.appendChild(e);
        return e;
    }

    // Info-bulle unique qui suit la souris
    const bulle = document.getElementById('statBulle');
    function montrer(evt, texte) {
        bulle.textContent = texte;
        bulle.style.display = 'block';
        const x = Math.min(evt.clientX + 14, window.innerWidth - bulle.offsetWidth - 8);
        bulle.style.left = x + 'px';
        bulle.style.top = (evt.clientY + 14) + 'px';
    }
    function cacher() { bulle.style.display = 'none'; }
    function survol(e, texte) {
        e.addEventListener('mousemove', ev => montrer(ev, texte));
        e.addEventListener('mouseleave', cacher);
    }

    function vide(conteneur, message) {
        conteneur.textContent = '';
        const p = document.createElement('p');
        p.className = 'text-muted text-center my-4';
        p.textContent = message || 'Aucune donnée.';
        conteneur.appendChild(p);
    }

    // ---------- Courbe lissée avec aire (évolution, comparée à la période précédente) ----------
    function lisser(pts) {
        let chemin = 'M' + pts[0].x + ',' + pts[0].y;
        for (let i = 0; i < pts.length - 1; i++) {
            const p0 = pts[i - 1] || pts[i], p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2] || p2, t = 0.18;
            const bas = Math.min(p1.y, p2.y), haut = Math.max(p1.y, p2.y);
            // La courbe ne dépasse jamais les deux points qu'elle relie (pas de creux sous zéro)
            const c1y = Math.min(haut, Math.max(bas, p1.y + (p2.y - p0.y) * t));
            const c2y = Math.min(haut, Math.max(bas, p2.y - (p3.y - p1.y) * t));
            chemin += ' C' + (p1.x + (p2.x - p0.x) * t) + ',' + c1y + ' ' + (p2.x - (p3.x - p1.x) * t) + ',' + c2y + ' ' + p2.x + ',' + p2.y;
        }
        return chemin;
    }

    function courbe(conteneur, serie, precedente) {
        conteneur.textContent = '';
        if (!serie.length) return vide(conteneur);
        const L = Math.max(conteneur.clientWidth, 320), H = 280;
        const m = { g: 40, d: 16, h: 16, b: 32 };
        const max = Math.max(1, ...serie.map(s => s.valeur), ...precedente.map(s => s.valeur));
        const pas = Math.max(1, Math.ceil(max / 4)), sommet = pas * 4;
        const n = serie.length;
        const x = i => n === 1 ? m.g + (L - m.g - m.d) / 2 : m.g + i * (L - m.g - m.d) / (n - 1);
        const y = v => H - m.b - v * (H - m.b - m.h) / sommet;

        const s = svg('svg', { viewBox: '0 0 ' + L + ' ' + H, width: L, height: H, class: 'stat-svg' }, conteneur);
        const defs = svg('defs', {}, s);
        const degrade = svg('linearGradient', { id: 'degradeAire', x1: 0, y1: 0, x2: 0, y2: 1 }, defs);
        svg('stop', { offset: '0%', 'stop-color': VERT, 'stop-opacity': 0.45 }, degrade);
        svg('stop', { offset: '100%', 'stop-color': VERT, 'stop-opacity': 0.02 }, degrade);

        // Grille horizontale et repères
        for (let k = 0; k <= 4; k++) {
            const v = k * pas;
            svg('line', { x1: m.g, x2: L - m.d, y1: y(v), y2: y(v), class: 'stat-grille' }, s);
            svg('text', { x: m.g - 8, y: y(v) + 4, 'text-anchor': 'end', class: 'stat-axe' }, s).textContent = v;
        }
        const saut = Math.ceil(n / Math.max(2, Math.floor((L - m.g) / 46)));
        serie.forEach((p, i) => {
            if (i % saut === 0) svg('text', { x: x(i), y: H - 10, 'text-anchor': 'middle', class: 'stat-axe' }, s).textContent = p.libelle;
        });

        // Période précédente en pointillés
        if (precedente.length === n && n > 1) {
            svg('path', { d: lisser(precedente.map((p, i) => ({ x: x(i), y: y(p.valeur) }))), class: 'stat-trait-prec' }, s);
        }

        const pts = serie.map((p, i) => ({ x: x(i), y: y(p.valeur) }));
        if (n > 1) {
            svg('path', { d: lisser(pts) + ' L' + x(n - 1) + ',' + y(0) + ' L' + x(0) + ',' + y(0) + ' Z', fill: 'url(#degradeAire)', class: 'stat-aire' }, s);
            svg('path', { d: lisser(pts), class: 'stat-trait', pathLength: 1 }, s);
        }

        // Survol : repère vertical, point et info-bulle pour chaque période
        const repere = svg('line', { y1: m.h, y2: y(0), class: 'stat-repere', visibility: 'hidden' }, s);
        const point = svg('circle', { r: 6, class: 'stat-point', visibility: 'hidden' }, s);
        serie.forEach((p, i) => {
            svg('circle', { cx: pts[i].x, cy: pts[i].y, r: 3.5, class: 'stat-point-fixe' }, s);
            const largeur = n === 1 ? L : (L - m.g - m.d) / (n - 1);
            const zone = svg('rect', { x: pts[i].x - largeur / 2, y: 0, width: largeur, height: H, fill: 'transparent' }, s);
            zone.addEventListener('mousemove', ev => {
                repere.setAttribute('x1', pts[i].x); repere.setAttribute('x2', pts[i].x); repere.setAttribute('visibility', 'visible');
                point.setAttribute('cx', pts[i].x); point.setAttribute('cy', pts[i].y); point.setAttribute('visibility', 'visible');
                let texte = p.libelle + ' : ' + p.valeur + ' dossier' + (p.valeur > 1 ? 's' : '');
                if (precedente.length === n) texte += '  (période précédente : ' + precedente[i].valeur + ')';
                montrer(ev, texte);
            });
            zone.addEventListener('mouseleave', () => { repere.setAttribute('visibility', 'hidden'); point.setAttribute('visibility', 'hidden'); cacher(); });
        });
    }

    // ---------- Anneau (donut) et camembert, avec légende ----------
    function pointSurCercle(cx, cy, r, angle) {
        const a = (angle - 90) * Math.PI / 180;
        return [cx + r * Math.cos(a), cy + r * Math.sin(a)];
    }
    function secteur(cx, cy, r, r0, a0, a1) {
        const fin = Math.min(a1, a0 + 359.99);
        const [x0, y0] = pointSurCercle(cx, cy, r, a0), [x1, y1] = pointSurCercle(cx, cy, r, fin);
        const grand = fin - a0 > 180 ? 1 : 0;
        if (r0 <= 0) return 'M' + cx + ',' + cy + ' L' + x0 + ',' + y0 + ' A' + r + ',' + r + ' 0 ' + grand + ' 1 ' + x1 + ',' + y1 + ' Z';
        const [x2, y2] = pointSurCercle(cx, cy, r0, fin), [x3, y3] = pointSurCercle(cx, cy, r0, a0);
        return 'M' + x0 + ',' + y0 + ' A' + r + ',' + r + ' 0 ' + grand + ' 1 ' + x1 + ',' + y1
            + ' L' + x2 + ',' + y2 + ' A' + r0 + ',' + r0 + ' 0 ' + grand + ' 0 ' + x3 + ',' + y3 + ' Z';
    }

    function anneau(conteneur, lignes, options) {
        conteneur.textContent = '';
        const total = lignes.reduce((t, l) => t + l.valeur, 0);
        if (!total) return vide(conteneur);
        const r0 = options.camembert ? 0 : 58;

        const bloc = document.createElement('div');
        bloc.className = 'stat-anneau';
        conteneur.appendChild(bloc);
        const s = svg('svg', { viewBox: '0 0 200 200', class: 'stat-svg-anneau' }, bloc);

        const legende = document.createElement('ul');
        legende.className = 'stat-legende';
        bloc.appendChild(legende);

        let angle = 0;
        lignes.forEach((l, i) => {
            const part = l.valeur / total, fin = angle + part * 360;
            const couleur = COULEURS[i % COULEURS.length];
            const pct = Math.round(part * 100);
            const chemin = svg('path', { d: secteur(100, 100, 92, r0, angle, fin), fill: couleur, class: 'stat-part' }, s);
            chemin.style.animationDelay = (i * 70) + 'ms';
            const milieu = pointSurCercle(0, 0, 6, (angle + fin) / 2);
            chemin.dataset.dx = milieu[0]; chemin.dataset.dy = milieu[1];
            const texte = l.libelle + ' : ' + l.valeur + ' (' + pct + ' %)';
            const li = document.createElement('li');
            const puce = document.createElement('span'); puce.className = 'stat-puce'; puce.style.background = couleur;
            const nom = document.createElement('span'); nom.className = 'stat-legende-nom'; nom.textContent = l.libelle;
            const val = document.createElement('span'); val.className = 'stat-legende-valeur'; val.textContent = pct + ' %';
            li.append(puce, nom, val);
            legende.appendChild(li);

            const actif = oui => {
                chemin.style.transform = oui ? 'translate(' + chemin.dataset.dx + 'px,' + chemin.dataset.dy + 'px)' : '';
                li.classList.toggle('actif', oui);
            };
            chemin.addEventListener('mouseenter', () => actif(true));
            chemin.addEventListener('mouseleave', () => actif(false));
            survol(chemin, texte);
            li.addEventListener('mouseenter', () => actif(true));
            li.addEventListener('mouseleave', () => actif(false));
            angle = fin;
        });

        if (!options.camembert) {
            svg('text', { x: 100, y: 104, 'text-anchor': 'middle', class: 'stat-centre-valeur' }, s).textContent = fr.format(total);
            svg('text', { x: 100, y: 122, 'text-anchor': 'middle', class: 'stat-centre-libelle' }, s).textContent = options.centre || '';
        }
    }

    // ---------- Jauge en demi-cercle (part des dossiers en cours) ----------
    function jauge(conteneur, valeur, total) {
        conteneur.textContent = '';
        if (!total) return vide(conteneur);
        const part = valeur / total;
        const s = svg('svg', { viewBox: '0 0 200 120', class: 'stat-svg-jauge' }, conteneur);
        const arc = (r, a) => {
            const [x0, y0] = pointSurCercle(100, 100, r, -90), [x1, y1] = pointSurCercle(100, 100, r, -90 + a);
            return 'M' + x0 + ',' + y0 + ' A' + r + ',' + r + ' 0 ' + (a > 180 ? 1 : 0) + ' 1 ' + x1 + ',' + y1;
        };
        svg('path', { d: arc(80, 180), class: 'stat-jauge-fond' }, s);
        if (part > 0) {
            const p = svg('path', { d: arc(80, Math.max(2, 180 * part)), class: 'stat-jauge-valeur', pathLength: 1 }, s);
            survol(p, valeur + ' dossier(s) en cours sur ' + total);
        }
        svg('text', { x: 100, y: 88, 'text-anchor': 'middle', class: 'stat-centre-valeur' }, s).textContent = Math.round(part * 100) + ' %';
        svg('text', { x: 100, y: 108, 'text-anchor': 'middle', class: 'stat-centre-libelle' }, s).textContent = 'en cours';
        svg('text', { x: 20, y: 116, 'text-anchor': 'middle', class: 'stat-axe' }, s).textContent = '0';
        svg('text', { x: 180, y: 116, 'text-anchor': 'middle', class: 'stat-axe' }, s).textContent = '100';
    }

    // ---------- Treemap : un rectangle par cité, d'aire proportionnelle au nombre de dossiers ----------
    function carroyer(items, x, y, w, h) {
        const total = items.reduce((t, i) => t + i.v, 0), echelle = w * h / total;
        const noeuds = items.map(i => Object.assign({}, i, { a: i.v * echelle }));
        const sortie = [];
        let zone = { x, y, w, h }, rang = [];
        const pire = (r, cote) => {
            const s = r.reduce((t, n) => t + n.a, 0), mx = Math.max(...r.map(n => n.a)), mn = Math.min(...r.map(n => n.a));
            return Math.max(cote * cote * mx / (s * s), s * s / (cote * cote * mn));
        };
        const poser = (r, z) => {
            const s = r.reduce((t, n) => t + n.a, 0);
            if (z.w >= z.h) {
                const rw = s / z.h; let cy = z.y;
                r.forEach(n => { const nh = n.a / rw; sortie.push(Object.assign({}, n, { x: z.x, y: cy, w: rw, h: nh })); cy += nh; });
                return { x: z.x + rw, y: z.y, w: z.w - rw, h: z.h };
            }
            const rh = s / z.w; let cx = z.x;
            r.forEach(n => { const nw = n.a / rh; sortie.push(Object.assign({}, n, { x: cx, y: z.y, w: nw, h: rh })); cx += nw; });
            return { x: z.x, y: z.y + rh, w: z.w, h: z.h - rh };
        };
        for (const n of noeuds) {
            const cote = Math.min(zone.w, zone.h);
            if (!rang.length || pire(rang.concat([n]), cote) <= pire(rang, cote)) rang.push(n);
            else { zone = poser(rang, zone); rang = [n]; }
        }
        if (rang.length) poser(rang, zone);
        return sortie;
    }

    function treemap(conteneur, lignes) {
        conteneur.textContent = '';
        const utiles = lignes.filter(l => l.valeur > 0);
        if (!utiles.length) return vide(conteneur);
        const L = conteneur.clientWidth, H = 280, total = utiles.reduce((t, l) => t + l.valeur, 0);
        const zone = document.createElement('div');
        zone.className = 'stat-treemap';
        zone.style.height = H + 'px';
        conteneur.appendChild(zone);
        carroyer(utiles.map((l, i) => ({ v: l.valeur, nom: l.libelle, i })), 0, 0, L, H).forEach((c, k) => {
            const e = document.createElement('div');
            e.className = 'stat-tuile';
            e.style.cssText = 'left:' + c.x + 'px;top:' + c.y + 'px;width:' + c.w + 'px;height:' + c.h + 'px;background:' + COULEURS[c.i % COULEURS.length] + ';animation-delay:' + (k * 60) + 'ms';
            const nom = document.createElement('span'); nom.className = 'stat-tuile-nom'; nom.textContent = c.nom;
            const val = document.createElement('span'); val.className = 'stat-tuile-valeur'; val.textContent = c.v;
            e.append(nom, val);
            survol(e, c.nom + ' : ' + c.v + ' (' + Math.round(c.v * 100 / total) + ' %)');
            zone.appendChild(e);
        });
    }

    // ---------- Radar : une branche par nuisance ----------
    function radar(conteneur, lignes) {
        conteneur.textContent = '';
        if (lignes.length < 3) return vide(conteneur, lignes.length ? 'Il faut au moins trois nuisances pour tracer le radar.' : 'Aucune donnée.');
        const n = lignes.length, max = Math.max(...lignes.map(l => l.valeur));
        const s = svg('svg', { viewBox: '0 0 360 300', class: 'stat-svg-radar' }, conteneur);
        const cx = 180, cy = 150, R = 95;
        const angle = i => i * 360 / n;
        for (let k = 1; k <= 4; k++) {
            const pts = lignes.map((l, i) => pointSurCercle(cx, cy, R * k / 4, angle(i)).join(',')).join(' ');
            svg('polygon', { points: pts, class: 'stat-grille-radar' }, s);
        }
        lignes.forEach((l, i) => {
            const [x, y] = pointSurCercle(cx, cy, R, angle(i));
            svg('line', { x1: cx, y1: cy, x2: x, y2: y, class: 'stat-grille' }, s);
            const [tx, ty] = pointSurCercle(cx, cy, R + 14, angle(i));
            const ancre = Math.abs(tx - cx) < 8 ? 'middle' : tx > cx ? 'start' : 'end';
            const nom = l.libelle.length > 18 ? l.libelle.slice(0, 17) + '…' : l.libelle;
            svg('text', { x: tx, y: ty + 4, 'text-anchor': ancre, class: 'stat-axe' }, s).textContent = nom;
        });
        const pts = lignes.map((l, i) => pointSurCercle(cx, cy, R * l.valeur / max, angle(i)));
        svg('polygon', { points: pts.map(p => p.join(',')).join(' '), class: 'stat-radar' }, s);
        pts.forEach((p, i) => {
            const c = svg('circle', { cx: p[0], cy: p[1], r: 5, class: 'stat-point-radar' }, s);
            survol(c, lignes[i].libelle + ' : ' + lignes[i].valeur);
        });
    }

    // ---------- Affichage (et nouveau dessin quand la fenêtre change de taille) ----------
    const $ = id => document.getElementById(id);
    function tout() {
        courbe($('grapheEvolution'), d.evolution, d.evolutionPrecedente || []);
        treemap($('grapheCites'), d.cites);
        radar($('grapheNuisances'), d.nuisances);
    }
    anneau($('grapheSecteurs'), d.secteurs, { centre: 'dossiers' });
    anneau($('grapheCategories'), d.categories, { camembert: true });
    jauge($('grapheEnCours'), d.enCours, d.total);
    tout();

    let minuterie;
    window.addEventListener('resize', () => { clearTimeout(minuterie); minuterie = setTimeout(tout, 150); });
})();
