// Page Statistiques : filtres croisés et graphiques cliquables, dessinés en SVG / HTML sans librairie externe.
(function () {
    const source = document.getElementById('donneesStats');
    const page = document.getElementById('statistiques');
    if (!source || !page) return;
    const depart = JSON.parse(source.textContent);
    const options = depart.options;
    let donnees = depart.resultat;
    let modeCourbe = 'dossiers';

    const NS = 'http://www.w3.org/2000/svg';
    const VERT = '#78be20';
    const COULEURS = ['#78be20', '#2d5068', '#1f9d8a', '#f39c12', '#8e6bbf', '#d9534f', '#4aa3df', '#a0aab4'];
    const fr = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 });
    const MOIS = Array.from({ length: 12 }, (_, i) => new Date(2000, i, 1).toLocaleDateString('fr-FR', { month: 'short' }));
    const $ = id => document.getElementById(id);

    // ---------- Filtres ----------
    // Une entrée par liste : clé de l'adresse, liste déroulante, libellé, et valeurs proposées
    const FILTRES = [
        { cle: 'secteur', select: 'fSecteur', libelle: 'Secteur', tous: 'Tous les secteurs' },
        { cle: 'cite', select: 'fCite', libelle: 'Cité', tous: 'Toutes les cités' },
        { cle: 'partieCommune', select: 'fPartieCommune', libelle: 'Partie commune', tous: 'Toutes les parties communes' },
        { cle: 'categorie', select: 'fCategorie', libelle: 'Catégorie', tous: 'Toutes les catégories' },
        { cle: 'sousCategorie', select: 'fSousCategorie', libelle: 'Sous-catégorie', tous: 'Toutes les sous-catégories' },
        { cle: 'nuisance', select: 'fNuisance', libelle: 'Nuisance', tous: 'Toutes les nuisances' }
    ];
    const etat = {};
    FILTRES.forEach(f => { const v = depart.filtres[f.cle]; etat[f.cle] = v == null ? '' : String(v); });
    // Période (date de début des faits) : premier et dernier mois « aaaa-mm », vides = sans limite de ce côté
    etat.debut = depart.filtres.debut || '';
    etat.fin = depart.filtres.fin || '';

    // Valeurs proposées pour une liste, selon les autres filtres (une cité dépend du secteur, une nuisance de la sous-catégorie...)
    function valeursDe(cle) {
        switch (cle) {
            case 'secteur': return options.secteurs;
            case 'cite': return etat.secteur ? options.cites.filter(c => c.parent === etat.secteur) : options.cites;
            case 'partieCommune': return options.partiesCommunes;
            case 'categorie': return options.categories;
            case 'sousCategorie': return etat.categorie ? options.sousCategories.filter(s => s.parent === etat.categorie) : options.sousCategories;
            case 'nuisance': {
                if (etat.sousCategorie) return options.nuisances.filter(n => n.parent === etat.sousCategorie);
                if (etat.categorie) {
                    const sous = new Set(valeursDe('sousCategorie').map(s => s.code));
                    return options.nuisances.filter(n => sous.has(n.parent));
                }
                return options.nuisances;
            }
        }
        return [];
    }
    function libelleDe(cle, code) {
        const v = valeursDe(cle).concat(cle === 'cite' ? options.cites : []).find(x => x.code === code);
        return v ? v.libelle : code;
    }

    // Remplit les listes ; une valeur qui n'est plus proposée (ex. cité d'un autre secteur) est retirée
    function majListes() {
        FILTRES.forEach(f => {
            const select = $(f.select);
            const valeurs = valeursDe(f.cle);
            if (etat[f.cle] && !valeurs.some(v => v.code === etat[f.cle])) etat[f.cle] = '';
            select.textContent = '';
            select.add(new Option(f.tous, ''));
            valeurs.forEach(v => select.add(new Option(v.libelle, v.code)));
            select.value = etat[f.cle];
            select.classList.toggle('stat-filtre-actif', !!etat[f.cle]);
        });
    }

    // Pastilles des filtres actifs, avec une croix pour retirer chacun
    function majPastilles() {
        const zone = $('filtresActifs');
        zone.textContent = '';
        const pastille = (texte, retirer) => {
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'stat-pastille';
            b.textContent = texte;
            const croix = document.createElement('span');
            croix.setAttribute('aria-hidden', 'true');
            croix.textContent = '×';
            b.appendChild(croix);
            b.title = 'Retirer ce filtre';
            b.addEventListener('click', retirer);
            zone.appendChild(b);
        };
        if (periodeActive() && moisProposes.length)
            pastille('Période : ' + libMois(debutEffectif()) + ' → ' + libMois(finEffective()), () => { etat.debut = etat.fin = ''; charger(); });
        FILTRES.filter(f => etat[f.cle]).forEach(f => pastille(f.libelle + ' : ' + libelleDe(f.cle, etat[f.cle]), () => changer(f.cle, '')));
        $('statReinitialiser').disabled = !FILTRES.some(f => etat[f.cle]) && !periodeActive();
    }

    function changer(cle, valeur) {
        etat[cle] = valeur;
        majListes();
        charger();
    }

    // Clic sur un élément d'un graphique : filtre sur cet élément, ou retire le filtre s'il est déjà choisi
    function basculer(cle, code) {
        if (!code) return;
        changer(cle, etat[cle] === code ? '' : code);
    }

    FILTRES.forEach(f => $(f.select).addEventListener('change', e => changer(f.cle, e.target.value)));
    $('statReinitialiser').addEventListener('click', () => { FILTRES.forEach(f => etat[f.cle] = ''); etat.debut = etat.fin = ''; majListes(); charger(); });

    // ---------- Chargement des chiffres ----------
    // Filtres choisis, sous forme de paramètres d'adresse ; l'adresse de la page les garde
    function majAdresse() {
        const params = new URLSearchParams();
        if (etat.debut) params.set('debut', etat.debut);
        if (etat.fin) params.set('fin', etat.fin);
        FILTRES.forEach(f => { if (etat[f.cle]) params.set(f.cle, etat[f.cle]); });
        const qs = params.toString();
        history.replaceState(null, '', location.pathname + (qs ? '?' + qs : ''));
        // Le bouton d'export Excel suit les mêmes filtres
        $('statExport').href = $('statExport').dataset.url + (qs ? '?' + qs : '');
        return qs;
    }

    let requete = null;
    async function charger() {
        const qs = majAdresse();
        majPastilles();

        if (requete) requete.abort();
        requete = new AbortController();
        $('statContenu').classList.add('stat-chargement');
        $('statErreur').hidden = true;
        try {
            const reponse = await fetch(page.dataset.url + (qs ? '?' + qs : ''), { signal: requete.signal, headers: { Accept: 'application/json' } });
            if (!reponse.ok || reponse.redirected) throw new Error('Réponse ' + reponse.status);
            donnees = await reponse.json();
            afficher();
        } catch (e) {
            if (e.name === 'AbortError') return;
            $('statErreur').textContent = 'Les statistiques n\'ont pas pu être chargées. Rechargez la page (la session a peut-être expiré).';
            $('statErreur').hidden = false;
        } finally {
            $('statContenu').classList.remove('stat-chargement');
        }
    }


    // ---------- Période : curseur à deux poignées, mois par mois ----------
    // Les bornes sont le premier et le dernier mois des dossiers qui répondent aux autres filtres : elles se resserrent
    // quand ces filtres réduisent la période couverte. Une poignée laissée sur sa borne = pas de limite de ce côté.
    let moisProposes = [];      // mois « aaaa-mm » du curseur, du plus ancien au plus récent
    const libMois = m => { const [a, mo] = m.split('-'); return MOIS[Number(mo) - 1] + ' ' + a; };
    function listeMois(min, max) {
        const liste = [];
        let [a, m] = min.split('-').map(Number);
        const [aFin, mFin] = max.split('-').map(Number);
        while (a < aFin || (a === aFin && m <= mFin)) {
            liste.push(a + '-' + String(m).padStart(2, '0'));
            if (++m > 12) { m = 1; a++; }
        }
        return liste;
    }
    // Position d'un mois sur le curseur (un mois hors des bornes est ramené à la borne la plus proche)
    function position(mois, defaut) {
        if (!mois) return defaut;
        const i = moisProposes.indexOf(mois);
        if (i >= 0) return i;
        return mois < moisProposes[0] ? 0 : moisProposes.length - 1;
    }
    const periodeActive = () => !!(etat.debut || etat.fin);
    // Premier et dernier mois réellement couverts par la période choisie
    const debutEffectif = () => moisProposes[position(etat.debut, 0)];
    const finEffective = () => moisProposes[position(etat.fin, moisProposes.length - 1)];

    function majTextePeriode(a, b) {
        $('periodeTexte').textContent = a === b ? libMois(moisProposes[a]) : libMois(moisProposes[a]) + ' → ' + libMois(moisProposes[b]);
        const total = Math.max(1, moisProposes.length - 1);
        $('periodePiste').style.left = (a / total * 100) + '%';
        $('periodePiste').style.right = (100 - b / total * 100) + '%';
    }

    // Recalcule le curseur à partir des bornes reçues ; une limite devenue inutile (sur ou au-delà d'une borne) est retirée
    function majPeriode() {
        moisProposes = donnees.periodeMin && donnees.periodeMax ? listeMois(donnees.periodeMin, donnees.periodeMax) : [];
        $('blocPeriode').hidden = moisProposes.length < 2;
        if (moisProposes.length < 2) return;
        if (etat.debut && etat.debut <= moisProposes[0]) etat.debut = '';
        if (etat.fin && etat.fin >= moisProposes[moisProposes.length - 1]) etat.fin = '';
        const debut = $('periodeDebut'), fin = $('periodeFin');
        debut.max = fin.max = moisProposes.length - 1;
        debut.value = position(etat.debut, 0);
        fin.value = position(etat.fin, moisProposes.length - 1);
        $('periodeMin').textContent = libMois(moisProposes[0]);
        $('periodeMax').textContent = libMois(moisProposes[moisProposes.length - 1]);
        majTextePeriode(Number(debut.value), Number(fin.value));
    }

    // Pendant le glissement : on empêche les poignées de se croiser et on met le texte à jour ; au relâchement : on recharge
    ['periodeDebut', 'periodeFin'].forEach(id => {
        $(id).addEventListener('input', () => {
            const debut = $('periodeDebut'), fin = $('periodeFin');
            if (Number(debut.value) > Number(fin.value)) {
                if (id === 'periodeDebut') debut.value = fin.value; else fin.value = debut.value;
            }
            majTextePeriode(Number(debut.value), Number(fin.value));
        });
        $(id).addEventListener('change', () => {
            const a = Number($('periodeDebut').value), b = Number($('periodeFin').value);
            etat.debut = a === 0 ? '' : moisProposes[a];
            etat.fin = b === moisProposes.length - 1 ? '' : moisProposes[b];
            charger();
        });
    });

    // Choisit une période depuis un graphique (une année, ou un seul mois), ramenée aux bornes du curseur ;
    // si elle est déjà choisie, on la retire
    function choisirPeriode(debut, fin) {
        if (moisProposes.length) {
            if (debut < moisProposes[0]) debut = moisProposes[0];
            if (fin > moisProposes[moisProposes.length - 1]) fin = moisProposes[moisProposes.length - 1];
        }
        const memePeriode = periodeActive() && debutEffectif() === debut && finEffective() === fin;
        etat.debut = memePeriode || debut === moisProposes[0] ? '' : debut;
        etat.fin = memePeriode || fin === moisProposes[moisProposes.length - 1] ? '' : fin;
        charger();
    }
    const choisirAnnee = annee => choisirPeriode(annee + '-01', annee + '-12');
    const choisirMois = (annee, mois) => { const m = annee + '-' + String(mois).padStart(2, '0'); choisirPeriode(m, m); };
    function cliquablePeriode(e, action) {
        e.classList.add('stat-cliquable');
        e.addEventListener('click', () => { cacher(); action(); });
    }
    // Vrai si le mois est dans la période choisie (pour le mettre en évidence dans le calendrier)
    const dansPeriode = (annee, mois) => {
        if (!periodeActive() || !moisProposes.length) return false;
        const m = annee + '-' + String(mois).padStart(2, '0');
        return m >= debutEffectif() && m <= finEffective();
    };

    // ---------- Outils de dessin ----------
    function svg(nom, attrs, parent) {
        const e = document.createElementNS(NS, nom);
        for (const k in attrs) e.setAttribute(k, attrs[k]);
        if (parent) parent.appendChild(e);
        return e;
    }

    const bulle = $('statBulle');
    function montrer(evt, texte) {
        bulle.textContent = texte;
        bulle.style.display = 'block';
        bulle.style.left = Math.min(evt.clientX + 14, window.innerWidth - bulle.offsetWidth - 8) + 'px';
        bulle.style.top = (evt.clientY + 14) + 'px';
    }
    function cacher() { bulle.style.display = 'none'; }
    function survol(e, texte) {
        e.addEventListener('mousemove', ev => montrer(ev, texte));
        e.addEventListener('mouseleave', cacher);
    }
    // Rend un élément cliquable pour filtrer (si l'élément a un code)
    function cliquable(e, cle, code) {
        if (!code) return;
        e.classList.add('stat-cliquable');
        e.addEventListener('click', () => { cacher(); basculer(cle, code); });
    }

    function vide(conteneur, message) {
        conteneur.textContent = '';
        const p = document.createElement('p');
        p.className = 'text-muted text-center my-4';
        p.textContent = message || 'Aucune donnée.';
        conteneur.appendChild(p);
    }

    const unite = () => modeCourbe === 'cout' ? ' €' : '';
    const pluriel = (n, mot) => fr.format(n) + ' ' + mot + (n > 1 ? 's' : '');

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
        const m = { g: 56, d: 16, h: 16, b: 32 };
        const max = Math.max(1, ...serie.map(s => s.valeur), ...precedente.map(s => s.valeur));
        const pas = Math.max(1, Math.ceil(max / 4)), sommet = pas * 4;
        const n = serie.length;
        const x = i => n === 1 ? m.g + (L - m.g - m.d) / 2 : m.g + i * (L - m.g - m.d) / (n - 1);
        const y = v => H - m.b - v * (H - m.b - m.h) / sommet;

        const s = svg('svg', { viewBox: '0 0 ' + L + ' ' + H, width: L, height: H, class: 'stat-svg' }, conteneur);
        const degrade = svg('linearGradient', { id: 'degradeAire', x1: 0, y1: 0, x2: 0, y2: 1 }, svg('defs', {}, s));
        svg('stop', { offset: '0%', 'stop-color': VERT, 'stop-opacity': 0.45 }, degrade);
        svg('stop', { offset: '100%', 'stop-color': VERT, 'stop-opacity': 0.02 }, degrade);

        for (let k = 0; k <= 4; k++) {
            const v = k * pas;
            svg('line', { x1: m.g, x2: L - m.d, y1: y(v), y2: y(v), class: 'stat-grille' }, s);
            svg('text', { x: m.g - 8, y: y(v) + 4, 'text-anchor': 'end', class: 'stat-axe' }, s).textContent = fr.format(v);
        }
        const saut = Math.ceil(n / Math.max(2, Math.floor((L - m.g) / 46)));
        serie.forEach((p, i) => {
            if (i % saut === 0) svg('text', { x: x(i), y: H - 10, 'text-anchor': 'middle', class: 'stat-axe' }, s).textContent = p.libelle;
        });

        if (precedente.length === n && n > 1)
            svg('path', { d: lisser(precedente.map((p, i) => ({ x: x(i), y: y(p.valeur) }))), class: 'stat-trait-prec' }, s);

        const pts = serie.map((p, i) => ({ x: x(i), y: y(p.valeur) }));
        if (n > 1) {
            svg('path', { d: lisser(pts) + ' L' + x(n - 1) + ',' + y(0) + ' L' + x(0) + ',' + y(0) + ' Z', fill: 'url(#degradeAire)', class: 'stat-aire' }, s);
            svg('path', { d: lisser(pts), class: 'stat-trait', pathLength: 1 }, s);
        }

        const repere = svg('line', { y1: m.h, y2: y(0), class: 'stat-repere', visibility: 'hidden' }, s);
        const point = svg('circle', { r: 6, class: 'stat-point', visibility: 'hidden' }, s);
        serie.forEach((p, i) => {
            svg('circle', { cx: pts[i].x, cy: pts[i].y, r: 3.5, class: 'stat-point-fixe' }, s);
            const largeur = n === 1 ? L : (L - m.g - m.d) / (n - 1);
            const zone = svg('rect', { x: pts[i].x - largeur / 2, y: 0, width: largeur, height: H, fill: 'transparent' }, s);
            zone.addEventListener('mousemove', ev => {
                repere.setAttribute('x1', pts[i].x); repere.setAttribute('x2', pts[i].x); repere.setAttribute('visibility', 'visible');
                point.setAttribute('cx', pts[i].x); point.setAttribute('cy', pts[i].y); point.setAttribute('visibility', 'visible');
                let texte = p.libelle + ' : ' + (modeCourbe === 'cout' ? fr.format(p.valeur) + ' €' : pluriel(p.valeur, 'dossier'));
                if (precedente.length === n) texte += '  (un an avant : ' + fr.format(precedente[i].valeur) + unite() + ')';
                montrer(ev, texte);
            });
            zone.addEventListener('mouseleave', () => { repere.setAttribute('visibility', 'hidden'); point.setAttribute('visibility', 'hidden'); cacher(); });
            // En vue année par année, cliquer sur une année la choisit comme période
            if (!donnees.mensuel) cliquablePeriode(zone, () => choisirAnnee(p.libelle));
        });
    }

    // ---------- Anneau (donut) cliquable, avec légende ----------
    function pointSurCercle(cx, cy, r, angle) {
        const a = (angle - 90) * Math.PI / 180;
        return [cx + r * Math.cos(a), cy + r * Math.sin(a)];
    }
    function secteur(cx, cy, r, r0, a0, a1) {
        const fin = Math.min(a1, a0 + 359.99);
        const [x0, y0] = pointSurCercle(cx, cy, r, a0), [x1, y1] = pointSurCercle(cx, cy, r, fin);
        const grand = fin - a0 > 180 ? 1 : 0;
        const [x2, y2] = pointSurCercle(cx, cy, r0, fin), [x3, y3] = pointSurCercle(cx, cy, r0, a0);
        return 'M' + x0 + ',' + y0 + ' A' + r + ',' + r + ' 0 ' + grand + ' 1 ' + x1 + ',' + y1
            + ' L' + x2 + ',' + y2 + ' A' + r0 + ',' + r0 + ' 0 ' + grand + ' 0 ' + x3 + ',' + y3 + ' Z';
    }

    function anneau(conteneur, lignes, cle, mot) {
        conteneur.textContent = '';
        const total = lignes.reduce((t, l) => t + l.valeur, 0);
        if (!total) return vide(conteneur);

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
            const choisi = l.code && etat[cle] === l.code;
            const chemin = svg('path', { d: secteur(100, 100, 92, 58, angle, fin), fill: couleur, class: 'stat-part' + (choisi ? ' selectionne' : '') }, s);
            chemin.style.animationDelay = (i * 70) + 'ms';
            const [dx, dy] = pointSurCercle(0, 0, 6, (angle + fin) / 2);

            const li = document.createElement('li');
            if (choisi) li.classList.add('selectionne');
            const puce = document.createElement('span'); puce.className = 'stat-puce'; puce.style.background = couleur;
            const nom = document.createElement('span'); nom.className = 'stat-legende-nom'; nom.textContent = l.libelle;
            const val = document.createElement('span'); val.className = 'stat-legende-valeur'; val.textContent = pct + ' %';
            li.append(puce, nom, val);
            legende.appendChild(li);

            const actif = oui => { chemin.style.transform = oui ? 'translate(' + dx + 'px,' + dy + 'px)' : ''; li.classList.toggle('actif', oui); };
            [chemin, li].forEach(e => {
                e.addEventListener('mouseenter', () => actif(true));
                e.addEventListener('mouseleave', () => actif(false));
                cliquable(e, cle, l.code);
            });
            survol(chemin, l.libelle + ' : ' + pluriel(l.valeur, mot) + ' (' + pct + ' %)');
            angle = fin;
        });

        svg('text', { x: 100, y: 104, 'text-anchor': 'middle', class: 'stat-centre-valeur' }, s).textContent = fr.format(total);
        svg('text', { x: 100, y: 122, 'text-anchor': 'middle', class: 'stat-centre-libelle' }, s).textContent = mot + 's';
    }

    // ---------- Carte proportionnelle des cités (cliquable) ----------
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

    function treemap(conteneur, lignes, cle) {
        conteneur.textContent = '';
        const utiles = lignes.filter(l => l.valeur > 0);
        if (!utiles.length) return vide(conteneur);
        const L = conteneur.clientWidth, H = 280, total = utiles.reduce((t, l) => t + l.valeur, 0);
        const zone = document.createElement('div');
        zone.className = 'stat-treemap';
        zone.style.height = H + 'px';
        conteneur.appendChild(zone);
        carroyer(utiles.map((l, i) => ({ v: l.valeur, nom: l.libelle, code: l.code, i })), 0, 0, L, H).forEach((c, k) => {
            const e = document.createElement('div');
            e.className = 'stat-tuile' + (etat[cle] === c.code ? ' selectionne' : '')
                + (c.w < 80 || c.h < 48 ? ' stat-tuile-petite' : '');     // case trop petite : le nom reste dans la bulle
            e.style.cssText = 'left:' + c.x + 'px;top:' + c.y + 'px;width:' + c.w + 'px;height:' + c.h + 'px;background:' + COULEURS[c.i % COULEURS.length] + ';animation-delay:' + (k * 60) + 'ms';
            const nom = document.createElement('span'); nom.className = 'stat-tuile-nom'; nom.textContent = c.nom;
            const val = document.createElement('span'); val.className = 'stat-tuile-valeur'; val.textContent = fr.format(c.v);
            e.append(nom, val);
            survol(e, c.nom + ' : ' + pluriel(c.v, 'dossier') + ' (' + Math.round(c.v * 100 / total) + ' %)');
            cliquable(e, cle, c.code);
            zone.appendChild(e);
        });
    }

    // ---------- Radar des nuisances (cliquable) ----------
    function radar(conteneur, lignes, cle) {
        conteneur.textContent = '';
        if (lignes.length < 3) {
            if (!lignes.length) return vide(conteneur);
            return barres(conteneur, lignes, cle);     // moins de trois branches : des barres sont plus lisibles
        }
        const n = lignes.length, max = Math.max(...lignes.map(l => l.valeur));
        const s = svg('svg', { viewBox: '0 0 360 300', class: 'stat-svg-radar' }, conteneur);
        const cx = 180, cy = 150, R = 95;
        const angle = i => i * 360 / n;
        for (let k = 1; k <= 4; k++)
            svg('polygon', { points: lignes.map((l, i) => pointSurCercle(cx, cy, R * k / 4, angle(i)).join(',')).join(' '), class: 'stat-grille-radar' }, s);
        lignes.forEach((l, i) => {
            const [x, y] = pointSurCercle(cx, cy, R, angle(i));
            svg('line', { x1: cx, y1: cy, x2: x, y2: y, class: 'stat-grille' }, s);
            const [tx, ty] = pointSurCercle(cx, cy, R + 14, angle(i));
            const ancre = Math.abs(tx - cx) < 8 ? 'middle' : tx > cx ? 'start' : 'end';
            const texte = svg('text', { x: tx, y: ty + 4, 'text-anchor': ancre, class: 'stat-axe' + (etat[cle] === l.code ? ' selectionne' : '') }, s);
            texte.textContent = l.libelle.length > 18 ? l.libelle.slice(0, 17) + '…' : l.libelle;
            cliquable(texte, cle, l.code);
        });
        const pts = lignes.map((l, i) => pointSurCercle(cx, cy, R * l.valeur / max, angle(i)));
        svg('polygon', { points: pts.map(p => p.join(',')).join(' '), class: 'stat-radar' }, s);
        pts.forEach((p, i) => {
            const c = svg('circle', { cx: p[0], cy: p[1], r: etat[cle] === lignes[i].code ? 7 : 5, class: 'stat-point-radar' }, s);
            survol(c, lignes[i].libelle + ' : ' + fr.format(lignes[i].valeur) + ' signalement' + (lignes[i].valeur > 1 ? 's' : ''));
            cliquable(c, cle, lignes[i].code);
        });
    }

    // ---------- Barres horizontales ----------
    function barres(conteneur, lignes, cle, unite) {
        conteneur.textContent = '';
        if (!lignes.length) return vide(conteneur);
        const max = Math.max(...lignes.map(l => l.valeur), 1);
        lignes.forEach((l, i) => {
            const ligne = document.createElement('div');
            ligne.className = 'stat-ligne' + (cle && etat[cle] === l.code ? ' selectionne' : '');
            const nom = document.createElement('span'); nom.className = 'stat-ligne-nom'; nom.textContent = l.libelle;
            const piste = document.createElement('div'); piste.className = 'stat-piste';
            const barre = document.createElement('div'); barre.className = 'stat-barre';
            barre.style.width = (l.valeur * 100 / max) + '%';
            barre.style.background = COULEURS[i % COULEURS.length];
            piste.appendChild(barre);
            const val = document.createElement('span'); val.className = 'stat-ligne-valeur'; val.textContent = fr.format(l.valeur) + (unite === '€' ? ' €' : '');
            ligne.append(nom, piste, val);
            survol(ligne, l.libelle + ' : ' + (unite === '€' ? fr.format(l.valeur) + ' €' : pluriel(l.valeur, 'dossier')));
            if (cle) cliquable(ligne, cle, l.code);
            conteneur.appendChild(ligne);
        });
    }

    // ---------- Calendrier : années en lignes, mois en colonnes (cliquer choisit l'année ou le mois comme période) ----------
    function calendrier(conteneur, cases) {
        conteneur.textContent = '';
        if (!cases.length) return vide(conteneur);
        const annees = [...new Set(cases.map(c => c.annee))].sort((a, b) => b - a);
        const max = Math.max(...cases.map(c => c.valeur));
        const valeur = (a, m) => (cases.find(c => c.annee === a && c.mois === m) || { valeur: 0 }).valeur;

        const grille = document.createElement('div');
        grille.className = 'stat-calendrier';
        grille.appendChild(document.createElement('span'));
        MOIS.forEach(m => { const t = document.createElement('span'); t.className = 'stat-cal-mois'; t.textContent = m; grille.appendChild(t); });
        annees.forEach(a => {
            const titre = document.createElement('span');
            titre.className = 'stat-cal-annee';
            titre.textContent = a;
            cliquablePeriode(titre, () => choisirAnnee(String(a)));
            grille.appendChild(titre);
            for (let m = 1; m <= 12; m++) {
                const v = valeur(a, m);
                const c = document.createElement('span');
                c.className = 'stat-cal-case' + (dansPeriode(a, m) ? ' selectionne' : '');
                const intensite = v / max;
                c.style.background = v ? 'rgba(120, 190, 32, ' + (0.18 + 0.82 * intensite) + ')' : '';
                if (intensite < 0.45) c.style.color = 'var(--mh-blue)';     // texte foncé sur les cases claires
                if (v) c.textContent = v;
                survol(c, MOIS[m - 1] + ' ' + a + ' : ' + pluriel(v, 'dossier'));
                cliquablePeriode(c, () => choisirMois(a, m));
                grille.appendChild(c);
            }
        });
        conteneur.appendChild(grille);
    }

    // ---------- Affichage de tous les graphiques ----------
    function majCourbe() {
        const cout = modeCourbe === 'cout';
        courbe($('grapheEvolution'), cout ? donnees.evolutionCout : donnees.evolution,
            (cout ? donnees.evolutionCoutPrecedente : donnees.evolutionPrecedente) || []);
    }

    function afficher() {
        const aucun = donnees.total === 0;
        $('statVide').hidden = !aucun;
        $('statContenu').hidden = aucun;
        majPeriode();     // les bornes du curseur viennent des données
        majAdresse();     // une limite retirée par majPeriode disparaît aussi de l'adresse
        majListes();
        majPastilles();
        if (aucun) return;

        $('kpiTotal').textContent = fr.format(donnees.total);
        $('kpiTotalLibelle').textContent = periodeActive() && moisProposes.length
            ? 'Dossiers de ' + libMois(debutEffectif()) + ' à ' + libMois(finEffective())
            : 'Dossiers, toute la période';
        $('kpiCout').textContent = fr.format(donnees.coutTotal) + ' €';
        $('kpiCoutMoyen').textContent = fr.format(donnees.coutMoyen) + ' €';
        $('kpiDetails').textContent = fr.format(donnees.nbDetails);

        $('titreEvolution').textContent = 'Évolution ' + (donnees.mensuel ? 'mensuelle' : 'annuelle');
        const legende = $('legendeEvolution');
        legende.textContent = '';
        const element = (classe, texte) => { const s = document.createElement('span'); const i = document.createElement('i'); if (classe) i.className = classe; s.append(i, texte); legende.appendChild(s); };
        element('', modeCourbe === 'cout' ? 'Coût' : 'Dossiers');
        if ((donnees.evolutionPrecedente || []).length) element('prec', 'Un an avant');

        majCourbe();
        dessinerCartes();
    }

    // ---------- Cartes de répartition : les graphiques principaux, remplacés quand les filtres les rendent sans intérêt ----------
    // type : forme du graphique ; cle : filtre posé quand on clique dessus ; inutileSi : filtres qui le rendent sans intérêt ;
    // remplacant : statistique proposée en priorité à sa place ; unite : '€' pour les montants (sinon des dossiers).
    const GRAPHIQUES = {
        secteurs: { titre: 'Par secteur', type: 'anneau', cle: 'secteur', inutileSi: ['secteur', 'cite'], remplacant: 'saisonnalite' },
        partiesCommunes: { titre: 'Par partie commune', type: 'anneau', cle: 'partieCommune', inutileSi: ['partieCommune'], remplacant: 'piecesJointes' },
        sousCategories: { titre: 'Par sous-catégorie', precision: 'signalements', type: 'anneau', cle: 'sousCategorie', mot: 'signalement', inutileSi: ['sousCategorie', 'nuisance'], remplacant: 'coutNuisances' },
        cites: { titre: 'Cités les plus touchées', type: 'treemap', cle: 'cite', inutileSi: ['cite'], remplacant: 'adresses' },
        nuisances: { titre: 'Nuisances les plus fréquentes', type: 'radar', cle: 'nuisance', inutileSi: ['nuisance'], remplacant: 'coutPartiesCommunes' },
        calendrier: { titre: 'Calendrier des dossiers', precision: 'toutes les années', type: 'calendrier', inutileSi: [], remplacant: 'coutCites' },
        rues: { titre: 'Rues les plus touchées', type: 'barres', inutileSi: [], remplacant: 'adresses' },
        // Statistiques de remplacement
        adresses: { titre: 'Adresses les plus touchées', type: 'barres', inutileSi: [] },
        saisonnalite: { titre: 'Saisonnalité', precision: 'dossiers par mois de l\'année', type: 'barres', inutileSi: [], inutile: () => donnees.nbMois <= 12 },
        piecesJointes: { titre: 'Pièces jointes par dossier', type: 'anneau', inutileSi: [] },
        coutCites: { titre: 'Coût par cité', type: 'barres', cle: 'cite', unite: '€', inutileSi: ['cite'] },
        coutPartiesCommunes: { titre: 'Coût par partie commune', type: 'barres', cle: 'partieCommune', unite: '€', inutileSi: ['partieCommune'] },
        coutNuisances: { titre: 'Coût par nuisance', type: 'barres', cle: 'nuisance', unite: '€', inutileSi: ['nuisance'] }
    };
    const PRINCIPAUX = ['secteurs', 'partiesCommunes', 'sousCategories', 'cites', 'nuisances', 'calendrier', 'rues'];
    const REMPLACANTS = ['adresses', 'saisonnalite', 'piecesJointes', 'coutCites', 'coutPartiesCommunes', 'coutNuisances'];

    // Un graphique est utile s'il a au moins deux valeurs à comparer et qu'aucun filtre ne l'a déjà réduit à un seul élément
    const utile = nom => (donnees[nom] || []).filter(l => (l.valeur ?? 0) > 0).length > 1
        && !GRAPHIQUES[nom].inutileSi.some(cle => etat[cle]) && !(GRAPHIQUES[nom].inutile && GRAPHIQUES[nom].inutile());

    // Liste des graphiques à afficher : chaque principal inutile cède sa place (même position) à son remplaçant préféré,
    // ou au premier remplaçant utile pas encore affiché
    function choisirGraphiques() {
        const choisis = [];
        PRINCIPAUX.forEach(nom => {
            if (utile(nom)) { choisis.push(nom); return; }
            const candidats = [GRAPHIQUES[nom].remplacant].concat(REMPLACANTS);
            const remplacant = candidats.find(c => c && !choisis.includes(c) && !PRINCIPAUX.includes(c) && utile(c));
            if (remplacant) choisis.push(remplacant);
        });
        return choisis;
    }

    function dessinerCartes() {
        const grille = $('statCartes');
        grille.textContent = '';
        // On crée d'abord toutes les cartes, puis on dessine : la largeur des graphiques dépend de la place prise par les autres
        const aDessiner = choisirGraphiques().map(nom => {
            const g = GRAPHIQUES[nom];
            const bloc = document.createElement('section');
            bloc.className = 'stat-bloc' + (g.type === 'treemap' || g.type === 'calendrier' ? ' stat-bloc-large' : '');
            const carte = document.createElement('div'); carte.className = 'card shadow-sm card-mh h-100';
            const entete = document.createElement('div'); entete.className = 'card-header h6 mb-0'; entete.textContent = g.titre;
            if (g.precision) { const p = document.createElement('span'); p.className = 'text-muted fw-normal small'; p.textContent = ' (' + g.precision + ')'; entete.appendChild(p); }
            const corps = document.createElement('div'); corps.className = 'card-body' + (g.type === 'radar' ? ' d-flex align-items-center' : '');
            const zone = document.createElement('div'); zone.className = 'w-100';
            corps.appendChild(zone); carte.append(entete, corps); bloc.appendChild(carte); grille.appendChild(bloc);
            return { nom, g, zone };
        });
        aDessiner.forEach(({ nom, g, zone }) => {
            const lignes = donnees[nom];
            if (g.type === 'anneau') anneau(zone, lignes, g.cle, g.mot || 'dossier');
            else if (g.type === 'treemap') treemap(zone, lignes, g.cle);
            else if (g.type === 'radar') radar(zone, lignes, g.cle);
            else if (g.type === 'calendrier') calendrier(zone, lignes);
            else barres(zone, lignes, g.cle, g.unite);
        });
    }

    // Bascule Dossiers / Coût de la courbe
    document.querySelectorAll('[data-mode]').forEach(b => b.addEventListener('click', () => {
        modeCourbe = b.dataset.mode;
        document.querySelectorAll('[data-mode]').forEach(x => x.classList.toggle('active', x === b));
        afficher();
    }));

    // Nouveau dessin quand la fenêtre change de taille (la largeur des graphiques en dépend)
    let minuterie;
    window.addEventListener('resize', () => {
        clearTimeout(minuterie);
        minuterie = setTimeout(() => {
            if (!donnees.total) return;
            majCourbe();
            dessinerCartes();
        }, 150);
    });

    afficher();
})();
