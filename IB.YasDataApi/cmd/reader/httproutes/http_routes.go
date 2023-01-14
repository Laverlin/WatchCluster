package httproutes

import (
	"IB.YasDataApi/abstract"
	"IB.YasDataApi/dal"
)

type HttpRoutes struct {
	Config abstract.Config
	DataLayer dal.Dal
}

func New(config abstract.Config, dataLayer dal.Dal) HttpRoutes {
	return HttpRoutes {
		Config: config,
		DataLayer: dataLayer,
	}
}