# 📚 MaBoutique - Architecture E-commerce .NET 8

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)
![Redis](https://img.shields.io/badge/Cache-Redis-red)
![Entity Framework](https://img.shields.io/badge/ORM-EF%20Core-blue)
![Architecture](https://img.shields.io/badge/Architecture-Razor%20Pages-green)

**MaBoutique** est une application web e-commerce moderne simulant une librairie en ligne.
Ce projet démontre l'intégration de technologies avancées pour la performance (**Redis**), la sécurité (**Identity**) et l'optimisation des ressources (**Cookies**).

---

## 🌟 Fonctionnalités Principales

### 🛒 Expérience Client 
* **Catalogue Dynamique :** Affichage des livres avec filtrage par catégories.
* **Panier  (Innovant) :**
    * Le panier n'est **pas stocké en base de données** mais sérialisé en **JSON dans un Cookie** sécurisé.
    * *Avantage :* Réduit la charge serveur et évite les tables SQL.
    * Persistance de 7 jours (le client retrouve son panier s'il revient).
    * Mise à jour dynamique des quantités (+/-) avec recalcul immédiat du total.
* **Système d'Avis :**
    * Les utilisateurs connectés peuvent noter les livres (1 à 5 étoiles).
    * Calcul automatique de la moyenne des notes.

### ⚡ Performance & Cache (Redis)
* **Stratégie "Cache-Aside" :**
    * À la première visite, les produits sont chargés depuis SQL et stockés dans **Redis**.
    * Aux visites suivantes, les données viennent de la RAM (Redis).
* **Invalidation Intelligente :**
    * Si un Admin modifie ou supprime un produit, le cache Redis concerné est automatiquement détruit pour garantir la fraîcheur des données.
* **Fail-Safe :** Si Redis tombe en panne, le site bascule automatiquement sur la base de données SQL sans planter.

### 🛡️ Administration & Sécurité 
* **Gestion des Rôles :**
    * **Admin :** Accès complet (CRUD Produits).
    * **User :** Accès limité (Achat, Notation).
* **Protection des Routes :** Les pages de création/édition sont protégées par l'attribut `[Authorize(Roles = "Admin")]`.
* **Menu Adaptatif :** Le lien "Administration" n'apparaît que pour les administrateurs.

---

## 🛠️ Stack Technique

| Domaine | Technologie | Détail |
| :--- | :--- | :--- |
| **Framework** | .NET 8 | ASP.NET Core Razor Pages |
| **Base de Données** | SQLite | Entity Framework Core (Code First) |
| **Cache** | Redis | StackExchange.Redis |
| **Authentification** | ASP.NET Identity | Gestion des Users et Roles |
| **Sérialisation** | System.Text.Json | Gestion du Panier Cookie |

---

## 🚀 Installation et Démarrage

### Prérequis
* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* [Redis](https://redis.io/) (via Docker ou Windows MSI)

### 1. Cloner le projet
```bash
git clone [https://github.com/bi-aya/MaBoutique-DotNet8.git](https://github.com/bi-aya/MaBoutique-DotNet8.git)
cd MaBoutique

### 2. Configurer la Base de Données
Appliquez les migrations pour générer le fichier app.db localement.
```bash
dotnet ef database update
3. Lancer Redis
Assurez-vous que votre serveur Redis tourne sur le port par défaut.
Windows : Lancer redis-server.exe
Docker : docker run -p 6379:6379 -d redis4.
Démarrer l'application
Bash
dotnet watch run
Accédez à l'URL indiquée (ex: https://localhost:7001).🔑
Comptes de Démonstration
Le système crée automatiquement un Administrateur au premier lancement (Seeding).
Rôle,Email,Mot de Passe
Administrateur,admin@maboutique.com,Admin123!
Utilisateur,"(À créer via ""S'inscrire"")",Au choix
👤 BISSOU AYA
Projet académique 2025.
