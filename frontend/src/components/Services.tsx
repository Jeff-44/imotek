const services = [
  {
    title: 'Louer',
    description: 'Trouvez une maison, un appartement ou un local adapté à vos besoins et à votre budget.',
    icon: 'M4 11 12 4l8 7M6 10v10h12V10M10 20v-5h4v5',
  },
  {
    title: 'Acheter',
    description: 'Explorez les biens à vendre et préparez sereinement votre projet d’acquisition.',
    icon: 'M12 3v18M17 7.5c0-1.9-2.2-3-5-3s-5 1.1-5 3 2.2 2.8 5 3.4 5 1.6 5 3.6-2.2 3-5 3-5-1.1-5-3',
  },
  {
    title: 'Publier un bien',
    description: 'Propriétaires et agents : présentez vos biens à vendre ou à louer à un large public.',
    icon: 'M12 5v14M5 12h14',
  },
]

export function Services() {
  return (
    <section id="services" className="section" aria-labelledby="services-title">
      <div className="container">
        <h2 id="services-title" className="section__title">
          Nos services immobiliers
        </h2>
        <p className="section__lead">
          Imotek accompagne locataires, acheteurs et propriétaires dans leurs projets partout en Haïti.
        </p>

        <ul className="services">
          {services.map((service) => (
            <li key={service.title} className="card">
              <span className="card__icon" aria-hidden="true">
                <svg viewBox="0 0 24 24" width="24" height="24">
                  <path
                    d={service.icon}
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
              </span>
              <h3 className="card__title">{service.title}</h3>
              <p className="card__text">{service.description}</p>
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}
