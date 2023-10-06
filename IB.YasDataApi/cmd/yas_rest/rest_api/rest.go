package rest_api

import (
	"IB.YasDataApi/abstract"
	"IB.YasDataApi/dal"
)

type Rest struct {
	Config abstract.Config
	DataLayer dal.Dal
}

func New(config abstract.Config, dataLayer dal.Dal) Rest {
	return Rest {
		Config: config,
		DataLayer: dataLayer,
	}
}